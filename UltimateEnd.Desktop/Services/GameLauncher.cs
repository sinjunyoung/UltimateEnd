using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Desktop.Models;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Desktop.Services
{
    public class GameLauncher(Action? onDeactivate = null, Action? onActivate = null) : IGameLauncher
    {
        private readonly Action? _onDeactivate = onDeactivate;
        private readonly Action? _onActivate = onActivate;
        private readonly AppProvider _appProvider = new();

        public async Task<EmulatorValidationResult> ValidateEmulatorAsync(GameMetadata game)
        {
            if (IsNativeApp(game)) return ValidateNativeApp(game);

            try
            {
                var command = GetEmulatorCommand(game.PlatformId!, game.EmulatorId);

                if (UriHelper.IsUriScheme(command.Executable)) return new EmulatorValidationResult { IsValid = true };

                string executable = Path.IsPathRooted(command.Executable)
                    ? command.Executable
                    : Path.Combine(AppContext.BaseDirectory, command.Executable);

                if (!File.Exists(executable))
                {
                    var downloadUrl = await EmulatorUrlProvider.Instance.GetEmulatorDownloadUrlAsync(command.Id);

                    return new EmulatorValidationResult
                    {
                        IsValid = false,
                        ErrorType = EmulatorErrorType.ExecutableNotFound,
                        PlatformId = game.PlatformId,
                        EmulatorId = game.EmulatorId ?? command.Id,
                        EmulatorName = command.Name,
                        MissingPath = executable,
                        ErrorMessage = $"에뮬레이터 실행 파일을 찾을 수 없습니다.",
                        CanInstall = !string.IsNullOrEmpty(downloadUrl),
                        DownloadUrl = downloadUrl
                    };
                }

                if (command.IsRetroArch)
                {
                    var coreDir = Path.Combine(Path.GetDirectoryName(executable) ?? string.Empty, "cores");
                    var corePath = Path.Combine(coreDir, $"{command.CoreName}_libretro.dll");

                    if (!File.Exists(corePath))
                    {
                        var downloadUrl = await EmulatorUrlProvider.Instance.GetCoreDownloadUrlAsync(command.CoreName!);

                        return new EmulatorValidationResult
                        {
                            IsValid = false,
                            ErrorType = EmulatorErrorType.CoreNotFound,
                            PlatformId = game.PlatformId,
                            EmulatorId = game.EmulatorId ?? command.Id,
                            EmulatorName = command.Name,
                            CoreName = command.CoreName,
                            MissingPath = corePath,
                            ErrorMessage = $"RetroArch 코어를 찾을 수 없습니다: {command.CoreName}",
                            CanInstall = !string.IsNullOrEmpty(downloadUrl),
                            DownloadUrl = downloadUrl
                        };
                    }
                }

                return new EmulatorValidationResult { IsValid = true };
            }
            catch (NotSupportedException)
            {
                return new EmulatorValidationResult
                {
                    IsValid = false,
                    ErrorType = EmulatorErrorType.NoSupportedEmulator,
                    PlatformId = game.PlatformId,
                    ErrorMessage = $"'{game.PlatformId}' 플랫폼을 지원하는 에뮬레이터가 없습니다.",
                    CanInstall = false
                };
            }
            catch (Exception ex)
            {
                return new EmulatorValidationResult
                {
                    IsValid = false,
                    ErrorType = EmulatorErrorType.Unknown,
                    ErrorMessage = ex.Message,
                    CanInstall = false
                };
            }
        }

        public async Task LaunchGameAsync(GameMetadata game)
        {
            if (IsNativeApp(game))
            {
                await LaunchNativeAppAsync(game);

                return;
            }

            var romPath = game.GetRomFullPath();

            bool gameStarted = false;

            try
            {
                var command = GetEmulatorCommand(game.PlatformId!, game.EmulatorId);

                await PlayTimeHistoryFactory.Instance.Update(romPath, PlayState.Start);

                gameStarted = true;

                await LaunchProcessAsync(command, romPath);
            }
            finally
            {
                if (gameStarted) await PlayTimeHistoryFactory.Instance.Update(romPath, PlayState.Stop);
            }
        }

        private bool IsNativeApp(GameMetadata game) => game.PlatformId?.Equals(_appProvider.PlatformId, StringComparison.OrdinalIgnoreCase) == true;

        private static EmulatorValidationResult ValidateNativeApp(GameMetadata game)
        {
            try
            {
                var exePath = game.GetRomFullPath();

                if (!File.Exists(exePath))
                {
                    return new EmulatorValidationResult
                    {
                        IsValid = false,
                        ErrorType = EmulatorErrorType.ExecutableNotFound,
                        PlatformId = game.PlatformId,
                        ErrorMessage = $"실행 파일을 찾을 수 없습니다: {exePath}",
                        MissingPath = exePath,
                        CanInstall = false
                    };
                }

                return new EmulatorValidationResult { IsValid = true };
            }
            catch (Exception ex)
            {
                return new EmulatorValidationResult
                {
                    IsValid = false,
                    ErrorType = EmulatorErrorType.Unknown,
                    ErrorMessage = ex.Message,
                    CanInstall = false
                };
            }
        }

        private async Task LaunchNativeAppAsync(GameMetadata game)
        {
            var exePath = game.GetRomFullPath();
            bool gameStarted = false;

            try
            {
                await PlayTimeHistoryFactory.Instance.Update(exePath, PlayState.Start);

                gameStarted = true;

                _onDeactivate?.Invoke();
                _appProvider.LaunchApp(game);

                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"앱 실행에 실패했습니다: {ex.Message}", ex);
            }
            finally
            {
                if (gameStarted)
                    await PlayTimeHistoryFactory.Instance.Update(exePath, PlayState.Stop);

                _onActivate?.Invoke();
            }
        }

        private static Command GetEmulatorCommand(string platformId, string? emulatorId = null)
        {
            var service = (CommandConfigServiceFactory.Create?.Invoke()) ?? throw new InvalidOperationException("CommandConfigService not initialized.");
            var config = service.LoadConfig();

            if (!string.IsNullOrEmpty(emulatorId))
            {
                if (config.Emulators.TryGetValue(emulatorId, out var specifiedEmulator))
                {
                    if (specifiedEmulator is Command specifiedCommand) return specifiedCommand;
                    else throw new InvalidOperationException("잘못된 에뮬레이터 명령 타입입니다.");
                }

                throw new InvalidOperationException($"에뮬레이터 '{emulatorId}'를 설정에서 찾을 수 없습니다.");
            }

            var normalizedPlatformId = PlatformInfoService.NormalizePlatformId(platformId);

            if (config.DefaultEmulators.TryGetValue(normalizedPlatformId, out string? defaultEmulatorId))
            {
                if (!config.Emulators.ContainsKey(defaultEmulatorId))
                    defaultEmulatorId = null;
            }

            if (string.IsNullOrEmpty(defaultEmulatorId))
            {
                var supportedEmulators = config.Emulators.Values
                    .Where(e => e.SupportedPlatforms
                    .Select(p => PlatformInfoService.NormalizePlatformId(p))
                    .Contains(normalizedPlatformId))
                    .OrderBy(e => e.Name.Contains("RetroArch", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                defaultEmulatorId = supportedEmulators.FirstOrDefault()?.Id;
            }

            if (string.IsNullOrEmpty(defaultEmulatorId)) throw new NotSupportedException($"'{platformId}' 플랫폼을 지원하는 에뮬레이터가 없습니다.");

            if (!config.Emulators.TryGetValue(defaultEmulatorId, out var emulatorCommand)) throw new InvalidOperationException($"에뮬레이터 '{defaultEmulatorId}'를 설정에서 찾을 수 없습니다.");

            if (emulatorCommand is not Command command) throw new InvalidOperationException("잘못된 에뮬레이터 명령 타입입니다.");

            return command;
        }

        private async Task LaunchProcessAsync(Command command, string romPath)
        {
            bool isUriScheme = UriHelper.IsUriScheme(command.Executable);
            string executable = isUriScheme ? command.Executable : (Path.IsPathRooted(command.Executable) ? command.Executable : Path.Combine(AppContext.BaseDirectory, command.Executable));

            if (!isUriScheme && !File.Exists(executable)) throw new FileNotFoundException($"에뮬레이터 실행 파일을 찾을 수 없습니다: {executable}");

            string arguments = BuildArguments(command, romPath, executable);
            string workingDir = !string.IsNullOrEmpty(command.WorkingDirectory) ? command.WorkingDirectory : Path.GetDirectoryName(executable) ?? string.Empty;

            var psi = new ProcessStartInfo
            {
                FileName = isUriScheme ? (executable + arguments) : $"\"{executable}\"",
                Arguments = isUriScheme ? string.Empty : arguments,
                UseShellExecute = isUriScheme,
                CreateNoWindow = !isUriScheme,
                WorkingDirectory = workingDir
            };

            Process? process = null;

            try
            {
                _onDeactivate?.Invoke();

                process = Process.Start(psi);

                if (process == null) throw new InvalidOperationException("프로세스를 시작할 수 없습니다.");

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("게임 실행에 실패했습니다.", ex);
            }
            finally
            {
                process?.Dispose();
                _onActivate?.Invoke();
            }
        }

        private static string BuildArguments(Command command, string romPath, string executable)
        {
            string? corePath = null;

            if (command.IsRetroArch)
            {
                var coreDir = Path.Combine(Path.GetDirectoryName(executable) ?? string.Empty, "cores");
                corePath = Path.Combine(coreDir, $"{command.CoreName}_libretro.dll");

                if (!File.Exists(corePath)) throw new FileNotFoundException($"RetroArch {command.Name} 코어를 찾을 수 없습니다.", corePath);
            }

            return TemplateVariableManager.ReplaceTokens(command.Arguments, romPath, command.CoreName, corePath);
        }
    }
}