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
        private const int BUFFER_SIZE = 262144;
        private const int DOWNLOAD_PROGRESS_INTERVAL = 5;
        private const int EXTRACT_PROGRESS_INTERVAL = 10;

        private static bool _sevenZipInitialized = false;
        private static readonly Lock _sevenZipLock = new();

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
            string message = $"{validation.EmulatorName} 에뮬레이터 실행 파일을 찾을 수 없습니다.\n\n필요한 경로: {validation.MissingPath}\n\n";

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

            message += "실행 파일을 직접 선택해주세요.";
            var confirmed = await ShowConfirmDialog("에뮬레이터 없음", message);

            return confirmed ? await SelectExecutableFile(validation) : EmulatorValidationAction.Cancel;
        }

        private static async Task<EmulatorValidationAction> HandleCoreNotFound(EmulatorValidationResult validation)
        {
            var confirmed = await ShowConfirmDialog("RetroArch 코어 없음", $"RetroArch {validation.CoreName} 코어를 찾을 수 없습니다.\n\n코어를 다운로드하시겠습니까?");

            return confirmed ? await DownloadAndInstallCore(validation) : EmulatorValidationAction.Cancel;
        }

        private static async Task<EmulatorValidationAction> HandleNoSupportedEmulator(EmulatorValidationResult validation) => await ShowErrorAndCancel("지원하지 않는 플랫폼", $"{validation.PlatformId} 플랫폼을 지원하는 에뮬레이터가 없습니다.\n\n설정에서 에뮬레이터를 추가해주세요.");

        #endregion

        #region Manual Selection

        private static async Task<EmulatorValidationAction> SelectExecutableFile(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.EmulatorId)) return EmulatorValidationAction.Cancel;

            try
            {
                var selectedPath = await SelectFile("실행 파일", "*.exe");

                if (string.IsNullOrEmpty(selectedPath)) return EmulatorValidationAction.Cancel;

                UpdateEmulatorConfig(validation.EmulatorId, selectedPath);

                return EmulatorValidationAction.Retry;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("경로 설정 실패", $"경로 설정 중 오류가 발생했습니다:\n{ex.Message}");

                return EmulatorValidationAction.Cancel;
            }
        }

        #endregion

        #region Core Download and Install

        private static async Task<EmulatorValidationAction> DownloadAndInstallCore(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.CoreName) || string.IsNullOrEmpty(validation.MissingPath)) return EmulatorValidationAction.Cancel;

            var coresDirectory = Path.GetDirectoryName(validation.MissingPath);

            if (string.IsNullOrEmpty(coresDirectory)) return await ShowErrorAndCancel("경로 오류", "코어 디렉토리를 확인할 수 없습니다.");

            var coreUrl = GetCoreDownloadUrl(validation.CoreName);

            return await DownloadAndExtractArchive(coreUrl, coresDirectory, $"{validation.CoreName} 코어",
                async (tempZipPath, targetDir) =>
                {
                    Directory.CreateDirectory(targetDir);
                    using var archive = ZipFile.OpenRead(tempZipPath);

                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            var destPath = Path.Combine(targetDir, entry.Name);
                            await Task.Run(() => entry.ExtractToFile(destPath, overwrite: true));
                        }
                    }
                },
                () => Task.FromResult(EmulatorValidationAction.Retry)

            );
        }

        private static string GetCoreDownloadUrl(string coreName) => coreName.Equals("fbneo_crcskip", StringComparison.OrdinalIgnoreCase) ? 
            "https://github.com/sinjunyoung/FBNeo/releases/download/FBNeo/fbneo_crcskip_libretro.dll.zip" : 
            $"https://buildbot.libretro.com/nightly/windows/x86_64/latest/{coreName}_libretro.dll.zip";

        #endregion

        #region Emulator Download and Install

        private static async Task<EmulatorValidationAction> DownloadAndInstallEmulator(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.DownloadUrl) || string.IsNullOrEmpty(validation.EmulatorId)) return EmulatorValidationAction.Cancel;

            var installFolderName = GetInstallFolderName(validation.EmulatorId);
            var targetDirectory = Path.Combine(AppContext.BaseDirectory, "Emulators", installFolderName);

            if (Directory.Exists(targetDirectory) && Directory.GetFiles(targetDirectory, "*.exe", SearchOption.AllDirectories).Length > 0)
            {
                var useExisting = await ShowConfirmDialog("이미 설치됨", $"{validation.EmulatorName}이(가) 이미 설치되어 있습니다.\n\n기존 설치를 사용하시겠습니까?\n(아니오를 선택하면 재설치됩니다)");

                if (useExisting) return await UseExistingInstallation(validation, targetDirectory, installFolderName);

                CleanupDirectory(targetDirectory);
            }

            var extension = GetArchiveExtension(validation.DownloadUrl);

            var result = await DownloadAndExtractArchive(validation.DownloadUrl, targetDirectory, validation.EmulatorName ?? "에뮬레이터",
                async (archivePath, targetDir) =>
                {
                    Directory.CreateDirectory(targetDir);
                    await Task.Run(() => ExtractArchive(archivePath, targetDir));
                },
                async () => await FinalizeEmulatorInstallation(validation, targetDirectory, installFolderName), extension);

            if (result == EmulatorValidationAction.Retry && installFolderName.Equals("retroarch", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(validation.CoreName))
            {
                var exeFiles = Directory.GetFiles(targetDirectory, "retroarch.exe", SearchOption.AllDirectories);

                if (exeFiles.Length > 0)
                {
                    var retroarchDir = Path.GetDirectoryName(exeFiles[0]);
                    var coresDirectory = Path.Combine(retroarchDir!, "cores");
                    var corePath = Path.Combine(coresDirectory, $"{validation.CoreName}_libretro.dll");

                    var coreValidation = new EmulatorValidationResult
                    {
                        CoreName = validation.CoreName,
                        MissingPath = corePath
                    };

                    await DownloadAndInstallCore(coreValidation);
                }
            }

            return result;
        }

        private static async Task<EmulatorValidationAction> UseExistingInstallation(EmulatorValidationResult validation, string targetDirectory, string installFolderName)
        {
            var existingExes = Directory.GetFiles(targetDirectory, "*.exe", SearchOption.AllDirectories);
            var selectedExe = SelectBestExecutable(existingExes, validation.EmulatorName ?? "");

            if (selectedExe == null)
            {
                await ShowInfoDialog("실행 파일 선택", "기존 설치된 파일 중 에뮬레이터 실행 파일을 선택해주세요.");
                selectedExe = await SelectFile("실행 파일", "*.exe", targetDirectory);

                if (string.IsNullOrEmpty(selectedExe)) return EmulatorValidationAction.Cancel;
            }

            UpdateEmulatorConfig(validation.EmulatorId, selectedExe, installFolderName);

            return EmulatorValidationAction.Retry;
        }

        private static async Task<EmulatorValidationAction> FinalizeEmulatorInstallation(EmulatorValidationResult validation, string targetDirectory, string installFolderName)
        {
            var exeFiles = Directory.GetFiles(targetDirectory, "*.exe", SearchOption.AllDirectories);

            if (exeFiles.Length == 0)
            {
                CleanupDirectory(targetDirectory);

                return await ShowErrorAndCancel("설치 실패", "압축 파일 내에 실행 파일(.exe)을 찾을 수 없습니다.");

            }

            var selectedExe = SelectBestExecutable(exeFiles, validation.EmulatorName ?? "");

            if (selectedExe == null)
            {
                await ShowInfoDialog("실행 파일 선택", $"설치된 파일 중 에뮬레이터 실행 파일을 선택해주세요.\n\n설치 위치: {targetDirectory}");
                selectedExe = await SelectFile("실행 파일", "*.exe", targetDirectory);

                if (string.IsNullOrEmpty(selectedExe))
                {
                    CleanupDirectory(targetDirectory);

                    return await ShowErrorAndCancel("설치 취소", "실행 파일을 선택하지 않아 설치가 취소되었습니다.");
                }
            }

            UpdateEmulatorConfig(validation.EmulatorId, selectedExe, installFolderName);

            return EmulatorValidationAction.Retry;
        }

        #endregion

        #region Common Download and Extract

        private static async Task<EmulatorValidationAction> DownloadAndExtractArchive(string downloadUrl, string targetDirectory, string displayName, Func<string, string, Task> extractAction, Func<Task<EmulatorValidationAction>> onSuccessAction, string? archiveExtension = null)
        {
            string? tempArchivePath = null;

            try
            {
                var extension = archiveExtension ?? ".zip";
                tempArchivePath = Path.Combine(Path.GetTempPath(), $"download_{Guid.NewGuid()}{extension}");

                await DownloadFile(downloadUrl, tempArchivePath, displayName);
                await DialogService.Instance.UpdateLoading("설치 중...");
                await extractAction(tempArchivePath, targetDirectory);
                await DialogService.Instance.HideLoading();

                CleanupFile(tempArchivePath);

                return await onSuccessAction();
            }
            catch (HttpRequestException ex)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(tempArchivePath);
                CleanupDirectory(targetDirectory);

                return await ShowErrorAndCancel("다운로드 실패", $"인터넷 연결을 확인해주세요.\n\n{ex.Message}");
            }
            catch (TaskCanceledException)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(tempArchivePath);
                CleanupDirectory(targetDirectory);

                return await ShowErrorAndCancel("다운로드 실패", "다운로드 시간이 초과되었습니다.");
            }
            catch (Exception ex)
            {
                await DialogService.Instance.HideLoading();
                CleanupFile(tempArchivePath);
                CleanupDirectory(targetDirectory);

                return await ShowErrorAndCancel("설치 실패", $"설치 중 오류가 발생했습니다:\n{ex.Message}");
            }
        }

        private static async Task DownloadFile(string url, string destinationPath, string displayName)
        {
            await DialogService.Instance.ShowLoading($"{displayName} 다운로드 중 (0%)");

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BUFFER_SIZE);

            var buffer = new byte[BUFFER_SIZE];
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
                    if (percent != lastPercent && percent % DOWNLOAD_PROGRESS_INTERVAL == 0)
                    {
                        lastPercent = percent;
                        await DialogService.Instance.UpdateLoading($"{displayName} 다운로드 중 ({percent}%)");
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
                ExtractSevenZipWithProgress(archivePath, targetDirectory);
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

                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    entry.ExtractToFile(destPath, overwrite: true);
                }

                extractedCount++;
                var percent = (extractedCount * 100) / totalEntries;

                if (percent != lastPercent && percent % EXTRACT_PROGRESS_INTERVAL == 0)
                {
                    lastPercent = percent;
                    DialogService.Instance.UpdateLoading($"압축 해제 중 ({percent}%)").Wait();
                }
            }
        }

        private static void ExtractSevenZipWithProgress(string archivePath, string targetDirectory)
        {
            Initialize7ZipLibrary();
            using var extractor = new SevenZipExtractor(archivePath);

            int lastPercent = 0;
            extractor.Extracting += (sender, e) =>
            {
                if (e.PercentDone != lastPercent && e.PercentDone % EXTRACT_PROGRESS_INTERVAL == 0)
                {
                    lastPercent = e.PercentDone;
                    DialogService.Instance.UpdateLoading($"압축 해제 중 ({e.PercentDone}%)").Wait();
                }
            };

            extractor.ExtractArchive(targetDirectory);
        }

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
            bool isRetroArch = installFolderName?.Equals("retroarch", StringComparison.OrdinalIgnoreCase) ?? false;

            foreach (var kvp in config.Emulators)
            {
                if (kvp.Value is not Command cmd) continue;

                bool shouldUpdate = (isRetroArch && kvp.Key.StartsWith("retroarch_", StringComparison.OrdinalIgnoreCase)) || (!isRetroArch && kvp.Key.Equals(emulatorId, StringComparison.OrdinalIgnoreCase));

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

        private static async Task<string?> SelectFile(string typeName, string pattern, string? initialDirectory = null)
        {
            var filter = new FilePickerFileType(typeName) { Patterns = [pattern] };
            var selectedPath = await DialogHelper.OpenFileAsync(initialDirectory ?? string.Empty, [filter]);

            return !string.IsNullOrEmpty(selectedPath) && File.Exists(selectedPath) ? selectedPath : null;
        }

        private static string GetArchiveExtension(string url)
        {
            var uri = new Uri(url);
            var extension = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();

            return (extension == ".zip" || extension == ".7z" || extension == ".rar") ? extension : ".7z";
        }

        private static string GetInstallFolderName(string emulatorId) => emulatorId.StartsWith("retroarch_", StringComparison.OrdinalIgnoreCase) ? "retroarch" : emulatorId;

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
                string[] invalidKeywords = ["uninstall", "uninst", "setup", "install", "config", "updater", "launcher"];

                return !invalidKeywords.Any(keyword => fileName.Contains(keyword));
            }).ToArray();

            if (validFiles.Length == 1) return validFiles[0];
            if (validFiles.Length > 1) return validFiles.OrderBy(f => f.Length).First();

            return null;
        }

        private static void CleanupFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            try { if (File.Exists(filePath)) File.Delete(filePath); }
            catch { }
        }

        private static void CleanupDirectory(string? dirPath)
        {
            if (string.IsNullOrEmpty(dirPath)) return;
            try { if (Directory.Exists(dirPath)) Directory.Delete(dirPath, recursive: true); }
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