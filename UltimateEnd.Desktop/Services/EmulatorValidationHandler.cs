using Avalonia.Platform.Storage;
using SevenZip;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Desktop.Models;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Desktop.Services
{
    public class EmulatorValidationHandler : IEmulatorValidationHandler
    {
        public async Task<EmulatorValidationAction> HandleValidationFailedAsync(EmulatorValidationResult validation)
        {
            return validation.ErrorType switch
            {
                EmulatorErrorType.ExecutableNotFound => await HandleExecutableNotFound(validation),
                EmulatorErrorType.CoreNotFound => await HandleCoreNotFound(validation),
                EmulatorErrorType.NoSupportedEmulator => await HandleNoSupportedEmulator(validation),
                _ => await ShowErrorAndCancel("에뮬레이터 오류", validation.ErrorMessage ?? "알 수 없는 오류가 발생했습니다.")
            };
        }

        #region Error Handlers

        private static async Task<EmulatorValidationAction> HandleExecutableNotFound(EmulatorValidationResult validation)
        {
            string message = $"{validation.EmulatorName} 에뮬레이터 실행 파일을 찾을 수 없습니다.\n\n";
            message += $"필요한 경로: {validation.MissingPath}\n\n";

            if (validation.CanInstall)
            {
                message += "어떻게 하시겠습니까?";

                var result = await ShowThreeButtonDialog("에뮬레이터 없음", message, "경로 지정", "설치", "취소");

                return result switch
                {
                    0 => await SelectExecutableFile(validation),
                    1 => await DownloadAndInstallEmulator(validation),
                    _ => EmulatorValidationAction.Cancel
                };
            }
            else
            {
                message += "실행 파일을 직접 선택해주세요.";
                var confirmed = await ShowConfirmDialog("에뮬레이터 없음", message);

                return confirmed ? await SelectExecutableFile(validation) : EmulatorValidationAction.Cancel;
            }
        }

        private static async Task<EmulatorValidationAction> HandleCoreNotFound(EmulatorValidationResult validation)
        {
            string message = $"RetroArch {validation.CoreName} 코어를 찾을 수 없습니다.\n\n";
            message += "RetroArch를 실행하여 'Online Updater > Core Downloader'에서 코어를 다운로드하시겠습니까?";

            var confirmed = await ShowConfirmDialog("RetroArch 코어 없음", message);

            if (confirmed)
            {
                var retroarchPath = FindRetroArchExecutable(validation.MissingPath);

                if (!string.IsNullOrEmpty(retroarchPath) && File.Exists(retroarchPath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = retroarchPath,
                            UseShellExecute = true
                        });

                        await ShowInfoDialog("RetroArch 실행됨", "RetroArch가 실행되었습니다.\n\n'Online Updater > Core Downloader'에서 필요한 코어를 다운로드한 후\n게임을 다시 실행해주세요.");
                    }
                    catch (Exception ex)
                    {
                        await ShowErrorDialog("실행 실패", $"RetroArch 실행에 실패했습니다:\n{ex.Message}");
                    }
                }
                else
                {
                    await ShowErrorDialog("RetroArch 없음", "RetroArch 실행 파일을 찾을 수 없습니다.");
                }
            }

            return EmulatorValidationAction.Cancel;
        }

        private static async Task<EmulatorValidationAction> HandleNoSupportedEmulator(EmulatorValidationResult validation) => await ShowErrorAndCancel("지원하지 않는 플랫폼", $"{validation.PlatformId} 플랫폼을 지원하는 에뮬레이터가 없습니다.\n\n설정에서 에뮬레이터를 추가해주세요.");

        #endregion

        #region Manual Selection

        private static async Task<EmulatorValidationAction> SelectExecutableFile(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.EmulatorId)) return EmulatorValidationAction.Cancel;

            try
            {
                var exeFilter = new FilePickerFileType("실행 파일") { Patterns = ["*.exe"] };
                var selectedPath = await DialogHelper.OpenFileAsync(string.Empty, [exeFilter]);

                if (string.IsNullOrEmpty(selectedPath) || !File.Exists(selectedPath)) return EmulatorValidationAction.Cancel;

                UpdateEmulatorConfig(validation.EmulatorId, selectedPath);
                await ShowSuccessDialog("경로 설정 완료", "에뮬레이터 경로가 저장되었습니다.\n게임을 다시 실행해주세요.");

                return EmulatorValidationAction.Retry;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("경로 설정 실패", $"경로 설정 중 오류가 발생했습니다:\n{ex.Message}");

                return EmulatorValidationAction.Cancel;
            }
        }

        #endregion

        #region Download and Install

        private static async Task<EmulatorValidationAction> DownloadAndInstallEmulator(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.DownloadUrl) || string.IsNullOrEmpty(validation.EmulatorId)) return EmulatorValidationAction.Cancel;

            string? tempArchivePath = null;
            string? targetDirectory = null;

            try
            {
                var extension = GetArchiveExtension(validation.DownloadUrl);
                var installFolderName = GetInstallFolderName(validation.EmulatorId);

                tempArchivePath = Path.Combine(Path.GetTempPath(), $"emulator_{Guid.NewGuid()}{extension}");
                targetDirectory = Path.Combine(AppContext.BaseDirectory, "Emulators", installFolderName);

                if (Directory.Exists(targetDirectory) && Directory.GetFiles(targetDirectory, "*.exe", SearchOption.AllDirectories).Length > 0)
                {
                    var useExisting = await ShowConfirmDialog("이미 설치됨", $"{validation.EmulatorName}이(가) 이미 설치되어 있습니다.\n\n기존 설치를 사용하시겠습니까?\n(아니오를 선택하면 재설치됩니다)");

                    if (useExisting)
                        return await UseExistingInstallation(validation, targetDirectory, installFolderName);
                    else
                        CleanupDirectory(targetDirectory);
                }

                await DownloadFile(validation.DownloadUrl, tempArchivePath, validation.EmulatorName ?? "에뮬레이터");

                await DialogService.Instance.UpdateLoading("압축 해제 중...");
                Directory.CreateDirectory(targetDirectory);
                await Task.Run(() => ExtractArchive(tempArchivePath, targetDirectory));
                await DialogService.Instance.HideLoading();

                CleanupFile(tempArchivePath);
                tempArchivePath = null;

                var exeFiles = Directory.GetFiles(targetDirectory, "*.exe", SearchOption.AllDirectories);

                if (exeFiles.Length == 0)
                {
                    CleanupDirectory(targetDirectory);
                    await ShowErrorDialog("설치 실패", "압축 파일 내에 실행 파일(.exe)을 찾을 수 없습니다.");

                    return EmulatorValidationAction.Cancel;
                }

                var selectedExe = SelectBestExecutable(exeFiles, validation.EmulatorName ?? "");

                if (selectedExe == null)
                {
                    await ShowInfoDialog("실행 파일 선택", $"설치된 파일 중 에뮬레이터 실행 파일을 선택해주세요.\n\n설치 위치: {targetDirectory}");

                    var exeFilter = new FilePickerFileType("실행 파일") { Patterns = ["*.exe"] };
                    selectedExe = await DialogHelper.OpenFileAsync(targetDirectory, [exeFilter]);

                    if (string.IsNullOrEmpty(selectedExe) || !File.Exists(selectedExe))
                    {
                        CleanupDirectory(targetDirectory);
                        await ShowErrorDialog("설치 취소", "실행 파일을 선택하지 않아 설치가 취소되었습니다.");

                        return EmulatorValidationAction.Cancel;
                    }
                }

                UpdateEmulatorConfig(validation.EmulatorId, selectedExe, installFolderName);
                await ShowSuccessDialog("설치 완료", $"{validation.EmulatorName} 설치가 완료되었습니다.\n게임을 다시 실행해주세요.");

                return EmulatorValidationAction.Retry;
            }
            catch (HttpRequestException ex)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(tempArchivePath);
                CleanupDirectory(targetDirectory);
                await ShowErrorDialog("다운로드 실패", $"인터넷 연결을 확인해주세요.\n\n{ex.Message}");

                return EmulatorValidationAction.Cancel;
            }
            catch (TaskCanceledException)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(tempArchivePath);
                CleanupDirectory(targetDirectory);
                await ShowErrorDialog("다운로드 실패", "다운로드 시간이 초과되었습니다.");

                return EmulatorValidationAction.Cancel;
            }
            catch (Exception ex)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(tempArchivePath);
                CleanupDirectory(targetDirectory);
                await ShowErrorDialog("설치 실패", $"설치 중 오류가 발생했습니다:\n{ex.Message}");

                return EmulatorValidationAction.Cancel;
            }
        }

        private static async Task<EmulatorValidationAction> UseExistingInstallation(EmulatorValidationResult validation, string targetDirectory, string installFolderName)
        {
            var existingExes = Directory.GetFiles(targetDirectory, "*.exe", SearchOption.AllDirectories);
            var selectedExe = SelectBestExecutable(existingExes, validation.EmulatorName ?? "");

            if (selectedExe == null)
            {
                await ShowInfoDialog("실행 파일 선택", "기존 설치된 파일 중 에뮬레이터 실행 파일을 선택해주세요.");

                var exeFilter = new FilePickerFileType("실행 파일") { Patterns = ["*.exe"] };
                selectedExe = await DialogHelper.OpenFileAsync(targetDirectory, [exeFilter]);

                if (string.IsNullOrEmpty(selectedExe) || !File.Exists(selectedExe)) return EmulatorValidationAction.Cancel;
            }

            UpdateEmulatorConfig(validation.EmulatorId, selectedExe, installFolderName);
            await ShowSuccessDialog("설정 완료", "기존 설치를 사용하도록 설정되었습니다.\n게임을 다시 실행해주세요.");

            return EmulatorValidationAction.Retry;
        }

        private static async Task DownloadFile(string url, string destinationPath, string emulatorName)
        {
            await DialogService.Instance.ShowLoading($"{emulatorName} 다운로드 중 (0%)");

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 262144); // 256KB

            var buffer = new byte[262144]; // 256KB
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

        #endregion

        #region Archive Extraction

        private static void ExtractArchive(string archivePath, string targetDirectory)
        {
            var extension = Path.GetExtension(archivePath).ToLowerInvariant();

            if (extension == ".zip")
                ExtractZipWithProgress(archivePath, targetDirectory);
            else
            {
                Initialize7ZipLibrary();
                using var extractor = new SevenZipExtractor(archivePath);

                int lastPercent = 0;
                extractor.Extracting += (sender, e) =>
                {
                    if (e.PercentDone != lastPercent && e.PercentDone % 10 == 0)
                    {
                        lastPercent = e.PercentDone;
                        DialogService.Instance.UpdateLoading($"압축 해제 중 ({e.PercentDone}%)").Wait();
                    }
                };

                extractor.ExtractArchive(targetDirectory);
            }
        }

        private static void ExtractZipWithProgress(string zipPath, string targetDirectory)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var totalEntries = archive.Entries.Count;
            var extractedCount = 0;
            var lastPercent = 0;

            foreach (var entry in archive.Entries)
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    var destPath = Path.Combine(targetDirectory, entry.FullName);
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
        }

        private static bool _sevenZipInitialized = false;
        private static readonly Lock _sevenZipLock = new();

        private static void Initialize7ZipLibrary()
        {
            if (_sevenZipInitialized) return;

            lock (_sevenZipLock)
            {
                if (_sevenZipInitialized) return;

                var dllPath = Path.Combine(AppContext.BaseDirectory, "7za.dll");

                if (!File.Exists(dllPath)) throw new FileNotFoundException("7za.dll을 찾을 수 없습니다. 프로젝트 루트에 7za.dll을 포함시켜주세요.");

                SevenZipBase.SetLibraryPath(dllPath);

                _sevenZipInitialized = true;
            }
        }

        #endregion

        #region Configuration Update

        private static void UpdateEmulatorConfig(string emulatorId, string executablePath, string? installFolderName = null)
        {
            var configService = CommandConfigServiceFactory.Create?.Invoke();

            if (configService == null) return;

            var config = configService.LoadConfig();

            bool isRetroArch = !string.IsNullOrEmpty(installFolderName) && installFolderName.Equals("retroarch", StringComparison.OrdinalIgnoreCase);

            foreach (var kvp in config.Emulators)
            {
                if (kvp.Value is not Command cmd) continue;

                bool shouldUpdate = false;

                if (isRetroArch && kvp.Key.StartsWith("retroarch_", StringComparison.OrdinalIgnoreCase))
                    shouldUpdate = true;
                else if (!isRetroArch && kvp.Key.Equals(emulatorId, StringComparison.OrdinalIgnoreCase))
                    shouldUpdate = true;

                if (shouldUpdate)
                {
                    cmd.Executable = executablePath;
                    cmd.WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
                }
            }

            configService.SaveConfig(config);
        }

        #endregion

        #region Utility Methods

        private static string GetArchiveExtension(string url)
        {
            var uri = new Uri(url);
            var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();

            if (string.IsNullOrEmpty(extension) || (extension != ".zip" && extension != ".7z" && extension != ".rar")) extension = ".7z";

            return extension;
        }

        private static string GetInstallFolderName(string emulatorId)
        {
            if (!string.IsNullOrEmpty(emulatorId) && emulatorId.StartsWith("retroarch_", StringComparison.OrdinalIgnoreCase)) return "retroarch";

            return emulatorId;
        }

        private static string? SelectBestExecutable(string[] exeFiles, string emulatorName)
        {
            if (exeFiles.Length == 0) return null;

            if (exeFiles.Length == 1) return exeFiles[0];

            var exactMatch = exeFiles.FirstOrDefault(exe => Path.GetFileNameWithoutExtension(exe).Equals(emulatorName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null) return exactMatch;

            var nameContains = exeFiles.FirstOrDefault(exe => Path.GetFileNameWithoutExtension(exe).Contains(emulatorName, StringComparison.OrdinalIgnoreCase));

            if (nameContains != null) return nameContains;

            var validFiles = exeFiles.Where(exe =>
            {
                var fileName = Path.GetFileName(exe).ToLowerInvariant();

                return !fileName.Contains("uninstall") && !fileName.Contains("uninst") && !fileName.Contains("setup") && !fileName.Contains("install") && !fileName.Contains("config") && !fileName.Contains("updater") && !fileName.Contains("launcher");

            }).ToArray();

            if (validFiles.Length == 1) return validFiles[0];

            if (validFiles.Length > 1) return validFiles.OrderBy(f => f.Length).First();

            return null;
        }

        private static string? FindRetroArchExecutable(string? corePath)
        {
            if (string.IsNullOrEmpty(corePath)) return null;

            var coresDir = Path.GetDirectoryName(corePath);

            if (string.IsNullOrEmpty(coresDir)) return null;

            var retroarchDir = Path.GetDirectoryName(coresDir);

            if (string.IsNullOrEmpty(retroarchDir)) return null;

            var candidates = new[]
            {
                Path.Combine(retroarchDir, "retroarch.exe"),
                Path.Combine(retroarchDir, "RetroArch.exe"),
                Path.Combine(retroarchDir, "retroarch64.exe"),
                Path.Combine(retroarchDir, "RetroArch64.exe")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static void CleanupFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch { }
        }

        private static void CleanupDirectory(string? dirPath)
        {
            if (string.IsNullOrEmpty(dirPath)) return;

            try
            {
                if (Directory.Exists(dirPath)) Directory.Delete(dirPath, recursive: true);
            }
            catch { }
        }

        #endregion

        #region Dialog Helpers

        private static Task<int> ShowThreeButtonDialog(string title, string message, string button1, string button2, string button3) => DialogService.Instance.ShowThreeButton(title, message, button1, button2, button3);

        private static Task<bool> ShowConfirmDialog(string title, string message) => DialogService.Instance.ShowConfirm(title, message);

        private static Task ShowErrorDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Error);

        private static Task ShowSuccessDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Success);

        private static Task ShowInfoDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Info);

        private static async Task<EmulatorValidationAction> ShowErrorAndCancel(string title, string message)
        {
            await ShowErrorDialog(title, message);
            return EmulatorValidationAction.Cancel;
        }

        #endregion
    }
}