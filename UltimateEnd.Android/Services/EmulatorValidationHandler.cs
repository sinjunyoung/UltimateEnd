using Android.Content;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

        private async Task<EmulatorValidationAction> HandleAppNotInstalled(EmulatorValidationResult validation)
        {
            string message = $"{validation.EmulatorName} 앱이 설치되어 있지 않습니다.\n\n";

            if (validation.CanInstall)
            {
                message += "APK를 다운로드하여 설치하시겠습니까?";

                if (await ShowConfirmDialog("에뮬레이터 없음", message))
                    return await DownloadAndInstallApk(validation) ? EmulatorValidationAction.Retry : EmulatorValidationAction.Cancel;
            }
            else
            {
                message += "직접 설치해주세요.";

                await ShowErrorDialog("에뮬레이터 없음", message);
            }

            return EmulatorValidationAction.Cancel;
        }

        private async Task<EmulatorValidationAction> HandleNoSupportedEmulator(EmulatorValidationResult validation) => await ShowErrorAndCancel("지원하지 않는 플랫폼", $"{validation.PlatformId} 플랫폼을 지원하는 에뮬레이터가 없습니다.");

        private async Task<bool> DownloadAndInstallApk(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.DownloadUrl))
                return false;

            string downloadPath = string.Empty;

            try
            {
                var cacheDir = GetMainActivity().CacheDir?.AbsolutePath;

                if (string.IsNullOrEmpty(cacheDir))
                {
                    await ShowErrorDialog("다운로드 실패", "캐시 디렉토리를 찾을 수 없습니다.");

                    return false;
                }

                var isZip = validation.DownloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                var cachedPath = Path.Combine(cacheDir, $"{validation.EmulatorId}{(isZip ? ".zip" : ".apk")}");

                if (!isZip && File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 1024)
                {
                    InstallApk(cachedPath);
                    await ShowSuccessDialog("설치 시작", "APK 설치 화면이 열립니다.\n설치 후 게임을 다시 실행해주세요.");

                    return false;
                }

                downloadPath = cachedPath;
                ShowProgressDialog($"{validation.EmulatorName} 다운로드 중...");

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(validation.DownloadUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write);

                    await response.Content.CopyToAsync(fs);
                }

                HideProgressDialog();

                string apkPath = isZip ? await ExtractApkFromZip(downloadPath, cacheDir) : downloadPath;

                if (string.IsNullOrEmpty(apkPath))
                {
                    await ShowErrorDialog("설치 실패", "ZIP 파일에서 APK를 찾을 수 없습니다.");
                    CleanupFile(downloadPath);

                    return false;
                }

                InstallApk(apkPath);
                await ShowSuccessDialog("다운로드 완료", "APK 설치 화면이 열립니다.\n설치 후 게임을 다시 실행해주세요.");

                return false;
            }
            catch (HttpRequestException)
            {
                HideProgressDialog();
                CleanupFile(downloadPath);
                await ShowErrorDialog("다운로드 실패", "인터넷 연결을 확인해주세요.");

                return false;
            }
            catch (TaskCanceledException)
            {
                HideProgressDialog();
                CleanupFile(downloadPath);
                await ShowErrorDialog("다운로드 실패", "다운로드 시간이 초과되었습니다.");

                return false;
            }
            catch (Exception ex)
            {
                HideProgressDialog();
                CleanupFile(downloadPath);
                await ShowErrorDialog("다운로드 실패", $"오류가 발생했습니다:\n{ex.Message}");

                return false;
            }
        }

        private async Task<string?> ExtractApkFromZip(string zipPath, string extractDir)
        {
            try
            {
                if (!File.Exists(zipPath))
                    return null;

                var extractSubDir = Path.Combine(extractDir, "extracted_apk");

                if (Directory.Exists(extractSubDir))
                    Directory.Delete(extractSubDir, recursive: true);

                Directory.CreateDirectory(extractSubDir);

                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zipPath);

                    foreach (var entry in archive.Entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            var destPath = Path.Combine(extractSubDir, entry.FullName);
                            var destDir = Path.GetDirectoryName(destPath);

                            if (!string.IsNullOrEmpty(destDir))
                                Directory.CreateDirectory(destDir);

                            entry.ExtractToFile(destPath, overwrite: true);
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

        private void InstallApk(string apkPath)
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
            if (string.IsNullOrEmpty(filePath))
                return;

            try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
        }

        public static void CleanupOldApkFiles()
        {
            try
            {
                var cacheDir = MainActivity.Instance?.CacheDir?.AbsolutePath;

                if (string.IsNullOrEmpty(cacheDir) || !Directory.Exists(cacheDir))
                    return;

                var cleanupDate = DateTime.Now.AddDays(-7);

                foreach (var file in Directory.GetFiles(cacheDir, "*.apk").Concat(Directory.GetFiles(cacheDir, "*.zip")))
                    try { if (new FileInfo(file).LastWriteTime < cleanupDate) File.Delete(file); } catch { }

                var extractDir = Path.Combine(cacheDir, "extracted_apk");

                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, recursive: true);
            }
            catch { }
        }

        private global::Android.App.Activity GetMainActivity() => MainActivity.Instance ?? throw new InvalidOperationException("MainActivity를 찾을 수 없습니다.");

        private static Task<bool> ShowConfirmDialog(string title, string message) => DialogService.Instance.ShowConfirm(title, message);

        private static Task ShowErrorDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Error);

        private static Task ShowSuccessDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Success);

        private async Task<EmulatorValidationAction> ShowErrorAndCancel(string title, string message)
        {
            await ShowErrorDialog(title, message);
            return EmulatorValidationAction.Cancel;
        }

        private static void ShowProgressDialog(string message) { }
        private static void HideProgressDialog() { }
    }
}