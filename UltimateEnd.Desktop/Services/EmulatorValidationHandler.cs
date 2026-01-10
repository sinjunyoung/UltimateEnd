using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
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
            switch (validation.ErrorType)
            {
                case EmulatorErrorType.ExecutableNotFound:
                    return await HandleExecutableNotFound(validation);

                case EmulatorErrorType.CoreNotFound:
                    return await HandleCoreNotFound(validation);

                case EmulatorErrorType.NoSupportedEmulator:
                    return await HandleNoSupportedEmulator(validation);

                default:
                    {
                        await ShowErrorDialog("에뮬레이터 오류", validation.ErrorMessage ?? "알 수 없는 오류가 발생했습니다.");

                        return EmulatorValidationAction.Cancel;
                    }
            }
        }

        private async Task<EmulatorValidationAction> HandleExecutableNotFound(EmulatorValidationResult validation)
        {
            string message = $"{validation.EmulatorName} 에뮬레이터 실행 파일을 찾을 수 없습니다.\n\n";

            if (validation.CanInstall)
            {
                message += "어떻게 하시겠습니까?";

                var result = await ShowThreeButtonDialog("에뮬레이터 없음", message, "파일 선택", "자동 설치", "취소");

                switch (result)
                {
                    case 0: // 파일 선택
                        bool selected = await SelectExecutableFile(validation);
                        return selected ? EmulatorValidationAction.Retry : EmulatorValidationAction.Cancel;

                    case 1: // 자동 설치
                        bool installed = await DownloadAndInstallEmulator(validation);
                        return installed ? EmulatorValidationAction.Retry : EmulatorValidationAction.Cancel;

                    default: // 취소
                        return EmulatorValidationAction.Cancel;
                }
            }
            else
            {
                message += "실행 파일을 선택해주세요.";

                var result = await ShowConfirmDialog("에뮬레이터 없음", message);

                if (result)
                {
                    bool selected = await SelectExecutableFile(validation);

                    return selected ? EmulatorValidationAction.Retry : EmulatorValidationAction.Cancel;
                }

                return EmulatorValidationAction.Cancel;
            }
        }

        private async Task<EmulatorValidationAction> HandleCoreNotFound(EmulatorValidationResult validation)
        {
            string message = $"RetroArch {validation.CoreName} 코어를 찾을 수 없습니다.\n\n";

            if (validation.CanInstall)
            {
                message += "자동으로 다운로드하시겠습니까?";

                var result = await ShowConfirmDialog("코어 없음", message);

                if (result)
                {
                    bool installed = await DownloadRetroArchCore(validation);
                    return installed ? EmulatorValidationAction.Retry : EmulatorValidationAction.Cancel;
                }
            }
            else
            {
                message += "RetroArch를 실행하여 코어를 다운로드해주세요.";
                await ShowErrorDialog("코어 없음", message);
            }

            return EmulatorValidationAction.Cancel;
        }

        private async Task<EmulatorValidationAction> HandleNoSupportedEmulator(EmulatorValidationResult validation)
        {
            string message = $"{validation.PlatformId} 플랫폼을 지원하는 에뮬레이터가 없습니다.\n\n설정에서 에뮬레이터를 추가해주세요.";

            await ShowErrorDialog("지원하지 않는 플랫폼", message);

            return EmulatorValidationAction.Cancel;
        }

        private async Task<bool> SelectExecutableFile(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.EmulatorId))
                return false;

            try
            {
                var exeFilter = new FilePickerFileType("실행 파일")
                {
                    Patterns = ["*.exe"]
                };

                var selectedPath = await DialogHelper.OpenFileAsync(string.Empty, [exeFilter]);

                if (string.IsNullOrEmpty(selectedPath))
                    return false;

                var configService = CommandConfigServiceFactory.Create?.Invoke();

                if (configService == null)
                    return false;

                var config = configService.LoadConfig();

                if (config.Emulators.TryGetValue(validation.EmulatorId, out var emulator))
                {
                    if (emulator is Command cmd)
                    {
                        cmd.Executable = selectedPath;

                        if (string.IsNullOrEmpty(cmd.WorkingDirectory))
                            cmd.WorkingDirectory = Path.GetDirectoryName(selectedPath) ?? string.Empty;

                        configService.SaveConfig(config);
                        await ShowSuccessDialog("경로 설정 완료", "에뮬레이터 경로가 저장되었습니다.");

                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                await ShowErrorDialog("경로 설정 실패", $"경로 설정 중 오류가 발생했습니다:\n{ex.Message}");

                return false;
            }
        }

        private async Task<bool> DownloadAndInstallEmulator(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.DownloadUrl))
                return false;

            try
            {
                ShowProgressDialog($"{validation.EmulatorName} 다운로드 중...");

                var tempZipPath = Path.Combine(Path.GetTempPath(), $"emulator_{Guid.NewGuid()}.zip");
                var targetDirectory = Path.Combine(AppContext.BaseDirectory, "Emulators", validation.EmulatorId!);

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(validation.DownloadUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fs = new FileStream(tempZipPath, FileMode.Create);
                    await response.Content.CopyToAsync(fs);
                }

                UpdateProgressDialog("압축 해제 중...");

                Directory.CreateDirectory(targetDirectory);
                ZipFile.ExtractToDirectory(tempZipPath, targetDirectory, overwriteFiles: true);

                File.Delete(tempZipPath);

                var exeFiles = Directory.GetFiles(targetDirectory, "*.exe", SearchOption.AllDirectories);
                if (exeFiles.Length > 0)
                {
                    var configService = CommandConfigServiceFactory.Create?.Invoke();
                    if (configService != null)
                    {
                        var config = configService.LoadConfig();
                        if (config.Emulators.TryGetValue(validation.EmulatorId!, out var emulator) && emulator is Command cmd)
                        {
                            cmd.Executable = exeFiles[0];
                            cmd.WorkingDirectory = Path.GetDirectoryName(exeFiles[0]) ?? string.Empty;
                            configService.SaveConfig(config);
                        }
                    }
                }

                HideProgressDialog();
                await ShowSuccessDialog("설치 완료", $"{validation.EmulatorName} 설치가 완료되었습니다.");

                return true;
            }
            catch (Exception ex)
            {
                HideProgressDialog();
                await ShowErrorDialog("설치 실패", $"설치 중 오류가 발생했습니다:\n{ex.Message}");

                return false;
            }
        }

        private async Task<bool> DownloadRetroArchCore(EmulatorValidationResult validation)
        {
            if (string.IsNullOrEmpty(validation.DownloadUrl) || string.IsNullOrEmpty(validation.MissingPath))
                return false;

            try
            {
                ShowProgressDialog($"{validation.CoreName} 코어 다운로드 중...");

                var coreDirectory = Path.GetDirectoryName(validation.MissingPath);

                if (string.IsNullOrEmpty(coreDirectory))
                    return false;

                Directory.CreateDirectory(coreDirectory);

                var tempPath = Path.Combine(Path.GetTempPath(), $"core_{Guid.NewGuid()}.tmp");

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(validation.DownloadUrl);
                    response.EnsureSuccessStatusCode();

                    await using var fs = new FileStream(tempPath, FileMode.Create);
                    await response.Content.CopyToAsync(fs);
                }

                if (validation.DownloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    UpdateProgressDialog("압축 해제 중...");
                    ZipFile.ExtractToDirectory(tempPath, coreDirectory, overwriteFiles: true);
                }
                else
                    File.Copy(tempPath, validation.MissingPath, overwrite: true);

                File.Delete(tempPath);

                HideProgressDialog();
                await ShowSuccessDialog("다운로드 완료", $"{validation.CoreName} 코어가 설치되었습니다.");

                return true;
            }
            catch (Exception ex)
            {
                HideProgressDialog();
                await ShowErrorDialog("다운로드 실패", $"다운로드 중 오류가 발생했습니다:\n{ex.Message}");

                return false;
            }
        }

        private static Task<int> ShowThreeButtonDialog(string title, string message, string button1, string button2, string button3) => DialogService.Instance.ShowThreeButton(title, message, button1, button2, button3);

        private static Task<bool> ShowConfirmDialog(string title, string message) => DialogService.Instance.ShowConfirm(title, message);

        private static Task ShowErrorDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Error);

        private static Task ShowSuccessDialog(string title, string message) => DialogService.Instance.ShowMessage(title, message, MessageType.Success);

        private static void ShowProgressDialog(string message) { }

        private static void UpdateProgressDialog(string message) { }        

        private static void HideProgressDialog() { }
    }
}