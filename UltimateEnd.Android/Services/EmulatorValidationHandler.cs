using Android.Content;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class EmulatorValidationHandler : IEmulatorValidationHandler
    {
        public async Task<EmulatorValidationAction> HandleValidationFailedAsync(EmulatorValidationResult validation)
        {
            return validation.ErrorType switch
            {
                EmulatorErrorType.AppNotInstalled => await HandleAppNotInstalled(validation),
                EmulatorErrorType.NoSupportedEmulator => await HandleNoSupportedEmulator(validation),
                _ => await ShowErrorAndCancel("에뮬레이터 오류", validation.ErrorMessage ?? "알 수 없는 오류가 발생했습니다.")
            };
        }

        private static async Task<EmulatorValidationAction> HandleAppNotInstalled(EmulatorValidationResult validation)
        {
            string message = $"{validation.EmulatorName} 앱이 설치되어 있지 않습니다.\n\nAPK를 다운로드하여 설치하시겠습니까?";

            var confirmed = await ShowConfirmDialog("에뮬레이터 없음", message);

            if (!confirmed) return EmulatorValidationAction.Cancel;

            return await DownloadAndInstallApkWithCheck(validation);
        }

        private static async Task<EmulatorValidationAction> HandleNoSupportedEmulator(EmulatorValidationResult validation) => await ShowErrorAndCancel("지원하지 않는 플랫폼", $"{validation.PlatformId} 플랫폼을 지원하는 에뮬레이터가 없습니다.");

        private static async Task<EmulatorValidationAction> DownloadAndInstallApkWithCheck(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.EmulatorId)) return await ShowErrorAndCancel("설치 불가", "에뮬레이터 정보를 찾을 수 없습니다.");

            var emulatorIdForUrl = validation.EmulatorId.StartsWith("retroarch_", StringComparison.OrdinalIgnoreCase) ? "retroarch" : validation.EmulatorId;

            try
            {
                await DialogService.Instance.ShowLoading("URL 확인 중...");
                var downloadUrl = await EmulatorUrlProvider.Instance.GetEmulatorDownloadUrlAsync(emulatorIdForUrl);
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    await ShowErrorDialog("다운로드 불가", "다운로드 URL을 찾을 수 없습니다.");
                    return EmulatorValidationAction.Cancel;
                }

                return await DownloadAndInstallApk(validation, downloadUrl) ? EmulatorValidationAction.Retry : EmulatorValidationAction.Cancel;
            }
            catch (Exception ex)
            {
                return await ShowErrorAndCancel("다운로드 불가", $"다운로드 URL을 확인하는 중 오류가 발생했습니다:\n{ex.Message}");
            }            
            finally
            {
                await DialogService.Instance.HideLoading();
            }
        }

        private static async Task<bool> DownloadAndInstallApk(EmulatorValidationResult validation, string downloadUrl)
        {
            string downloadPath = string.Empty;

            try
            {
                var cacheDir = GetMainActivity().CacheDir?.AbsolutePath;

                if (string.IsNullOrEmpty(cacheDir))
                {
                    await ShowErrorDialog("다운로드 실패", "캐시 디렉토리를 찾을 수 없습니다.");
                    return false;
                }

                var isZip = downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                var cachedPath = Path.Combine(cacheDir, $"{validation.EmulatorId}{(isZip ? ".zip" : ".apk")}");

                if (!isZip && File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 1024)
                {
                    InstallApk(cachedPath);
                    await ShowSuccessDialog("설치 시작", "APK 설치 화면이 열립니다.\n설치 후 게임을 다시 실행해주세요.");
                    return false;
                }

                downloadPath = cachedPath;

                await DownloadFile(downloadUrl, downloadPath, validation.EmulatorName ?? "에뮬레이터");

                string apkPath = isZip ? await ExtractApkFromZip(downloadPath, cacheDir) : downloadPath;

                if (string.IsNullOrEmpty(apkPath))
                {
                    await DialogService.Instance.HideLoading();
                    await ShowErrorDialog("설치 실패", "ZIP 파일에서 APK를 찾을 수 없습니다.");
                    CleanupFile(downloadPath);
                    return false;
                }

                await DialogService.Instance.HideLoading();
                InstallApk(apkPath);
                await ShowSuccessDialog("다운로드 완료", "APK 설치 화면이 열립니다.\n설치 후 게임을 다시 실행해주세요.");

                return false;
            }
            catch (HttpRequestException)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(downloadPath);
                await ShowErrorDialog("다운로드 실패", "인터넷 연결을 확인해주세요.");
                return false;
            }
            catch (TaskCanceledException)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(downloadPath);
                await ShowErrorDialog("다운로드 실패", "다운로드 시간이 초과되었습니다.");
                return false;
            }
            catch (Exception ex)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(downloadPath);
                await ShowErrorDialog("다운로드 실패", $"오류가 발생했습니다:\n{ex.Message}");
                return false;
            }
        }

        private static async Task DownloadFile(string url, string destinationPath, string emulatorName)
        {
            await DialogService.Instance.ShowLoading($"{emulatorName} 다운로드 중 (0%)");

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 262144);

            var buffer = new byte[262144];
            long totalRead = 0;
            int bytesRead;
            int lastPercent = 0;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    var percent = (int)((totalRead * 100) / totalBytes);

                    if (percent != lastPercent && percent % 5 == 0)
                    {
                        lastPercent = percent;
                        await DialogService.Instance.UpdateLoading($"{emulatorName} 다운로드 중 ({percent}%)");
                    }
                }
            }
        }

        private static async Task<string?> ExtractApkFromZip(string zipPath, string extractDir)
        {
            try
            {
                if (!File.Exists(zipPath)) return null;

                var extractSubDir = Path.Combine(extractDir, "extracted_apk");

                if (Directory.Exists(extractSubDir)) Directory.Delete(extractSubDir, recursive: true);

                Directory.CreateDirectory(extractSubDir);

                await DialogService.Instance.UpdateLoading("압축 해제 중...");

                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zipPath);
                    var totalEntries = archive.Entries.Count;
                    var extractedCount = 0;
                    var lastPercent = 0;

                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            var destPath = Path.Combine(extractSubDir, entry.FullName);
                            var destDir = Path.GetDirectoryName(destPath);

                            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

                            entry.ExtractToFile(destPath, overwrite: true);
                        }

                        extractedCount++;
                        var percent = (extractedCount * 100) / totalEntries;

                        if (percent != lastPercent && percent % 10 == 0)
                        {
                            lastPercent = percent;
                            DialogService.Instance.UpdateLoading($"압축 해제 중 ({percent}%)").Wait();
                        }
                    }
                });

                var apkFiles = Directory.GetFiles(extractSubDir, "*.apk", SearchOption.AllDirectories);

                return apkFiles.Length > 0 ? apkFiles[0] : null;
            }
            catch
            {
                return null;
            }
        }

        private static void InstallApk(string apkPath)
        {
            var activity = GetMainActivity();
            var file = new Java.IO.File(apkPath);

            global::Android.Net.Uri apkUri;

            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.N)
            {
                var fileProviderClass = Java.Lang.Class.ForName("androidx.core.content.FileProvider");
                var method = fileProviderClass.GetMethod("getUriForFile",
                    Java.Lang.Class.FromType(typeof(Context)),
                    Java.Lang.Class.FromType(typeof(Java.Lang.String)),
                    Java.Lang.Class.FromType(typeof(Java.IO.File)));
                apkUri = (global::Android.Net.Uri)method.Invoke(null, activity, $"{activity.PackageName}.fileprovider", file);
            }
            else
            {
                apkUri = global::Android.Net.Uri.FromFile(file);
            }

            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
            activity.StartActivity(intent);
        }

        private static void CleanupFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
        }

        public static void CleanupOldApkFiles()
        {
            try
            {
                var cacheDir = MainActivity.Instance?.CacheDir?.AbsolutePath;

                if (string.IsNullOrEmpty(cacheDir) || !Directory.Exists(cacheDir)) return;

                var cleanupDate = DateTime.Now.AddDays(-7);

                foreach (var file in Directory.GetFiles(cacheDir, "*.apk").Concat(Directory.GetFiles(cacheDir, "*.zip")))
                    try { if (new FileInfo(file).LastWriteTime < cleanupDate) File.Delete(file); } catch { }

                var extractDir = Path.Combine(cacheDir, "extracted_apk");

                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
            }
            catch { }
        }

        private static MainActivity GetMainActivity() => MainActivity.Instance ?? throw new InvalidOperationException("MainActivity를 찾을 수 없습니다.");

        private static Task<bool> ShowConfirmDialog(string title, string message) => DialogService.Instance.ShowConfirm(title, message);

        private static Task ShowErrorDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Error);

        private static Task ShowSuccessDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Success);

        private static async Task<EmulatorValidationAction> ShowErrorAndCancel(string title, string message)
        {
            await ShowErrorDialog(title, message);
            return EmulatorValidationAction.Cancel;
        }
    }
}