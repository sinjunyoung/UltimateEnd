using Android.Content;
using Android.Views;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Android.Models;
using UltimateEnd.Coordinators;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class GameLauncher : IGameLauncher
    {
        private readonly AppValidator _appValidator;
        private readonly IntentBuilder _intentBuilder;
        private readonly AppProvider _appProvider;

        public GameLauncher()
        {
            var context = GetMainActivityInstance();
            _appValidator = new AppValidator(context);
            _intentBuilder = new IntentBuilder(context);
            _appProvider = new AppProvider();
        }

        public async Task<EmulatorValidationResult> ValidateEmulatorAsync(GameMetadata game)
        {
            if (IsNativeApp(game)) return ValidateNativeApp(game);

            try
            {
                var command = GetEmulatorCommand(game.PlatformId!, game.EmulatorId);
                var packageName = AppValidator.ExtractPackageName(command.LaunchCommand);

                if (string.IsNullOrEmpty(packageName))
                {
                    return new EmulatorValidationResult
                    {
                        IsValid = false,
                        ErrorType = EmulatorErrorType.Unknown,
                        EmulatorName = command.Name,
                        ErrorMessage = "에뮬레이터 설정에서 패키지명을 찾을 수 없습니다.",
                        CanInstall = false
                    };
                }

                if (!_appValidator.IsAppInstalled(packageName))
                {
                    var emulatorIdForUrl = command.Id.StartsWith("retroarch_", StringComparison.OrdinalIgnoreCase) ? "retroarch" : command.Id;
                    await DialogService.Instance.ShowLoading("정보 확인 중...");
                    var downloadUrl = await EmulatorUrlProvider.Instance.GetEmulatorDownloadUrlAsync(emulatorIdForUrl);
                    await DialogService.Instance.HideLoading();

                    return new EmulatorValidationResult
                    {
                        IsValid = false,
                        ErrorType = EmulatorErrorType.AppNotInstalled,
                        PlatformId = game.PlatformId,
                        EmulatorId = game.EmulatorId ?? command.Id,
                        EmulatorName = command.Name,
                        ErrorMessage = $"{command.Name} 앱이 설치되어 있지 않습니다.",
                        CanInstall = !string.IsNullOrEmpty(downloadUrl),
                        DownloadUrl = downloadUrl
                    };
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
            var converter = PathConverterFactory.Create?.Invoke();
            var friendlyPath = converter?.RealPathToFriendlyPath(romPath) ?? romPath;

            bool gameStarted = false;

            try
            {
                var command = GetEmulatorCommand(game.PlatformId!, game.EmulatorId);
                ValidateRomPath(romPath);

                var intent = await _intentBuilder.BuildAsync(command, romPath);

                await PlayTimeHistoryFactory.Instance.Update(friendlyPath, PlayState.Start);
                gameStarted = true;

                await LaunchIntent(intent);

                await PlayTimeHistoryFactory.Instance.Update(friendlyPath, PlayState.Stop);
            }
            catch (ActivityNotFoundException ex)
            {
                if (gameStarted)
                    await PlayTimeHistoryFactory.Instance.Update(friendlyPath, PlayState.Stop);

                throw new InvalidOperationException("에뮬레이터 앱이 설치되어 있지 않습니다.", ex);
            }
            catch (Exception ex)
            {
                if (gameStarted)
                    await PlayTimeHistoryFactory.Instance.Update(friendlyPath, PlayState.Stop);

                throw new InvalidOperationException($"게임 실행에 실패했습니다: {ex.Message}", ex);
            }
        }

        private bool IsNativeApp(GameMetadata game) => game.PlatformId?.Equals(_appProvider.PlatformId, StringComparison.OrdinalIgnoreCase) == true;

        private EmulatorValidationResult ValidateNativeApp(GameMetadata game)
        {
            try
            {
                var romPath = game.GetRomFullPath();

                if (!File.Exists(romPath))
                {
                    return new EmulatorValidationResult
                    {
                        IsValid = false,
                        ErrorType = EmulatorErrorType.Unknown,
                        PlatformId = game.PlatformId,
                        ErrorMessage = "앱 파일을 찾을 수 없습니다.",
                        CanInstall = false
                    };
                }

                var fileContent = File.ReadAllText(romPath);
                var parts = fileContent.Split('|');
                var packageName = parts.Length > 0 ? parts[0] : string.Empty;

                if (string.IsNullOrEmpty(packageName))
                {
                    return new EmulatorValidationResult
                    {
                        IsValid = false,
                        ErrorType = EmulatorErrorType.Unknown,
                        PlatformId = game.PlatformId,
                        ErrorMessage = "앱 패키지명이 비어있습니다.",
                        CanInstall = false
                    };
                }

                if (!_appValidator.IsAppInstalled(packageName))
                {
                    return new EmulatorValidationResult
                    {
                        IsValid = false,
                        ErrorType = EmulatorErrorType.AppNotInstalled,
                        PlatformId = game.PlatformId,
                        ErrorMessage = $"앱이 설치되어 있지 않습니다: {packageName}",
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
            bool gameStarted = false;

            try
            {
                var romPath = game.GetRomFullPath();
                var fileContent = File.ReadAllText(romPath);
                var parts = fileContent.Split('|');
                var packageName = parts.Length > 0 ? parts[0] : string.Empty;
                var activityName = parts.Length > 1 ? parts[1] : string.Empty;

                var tempGame = new GameMetadata
                {
                    PlatformId = game.PlatformId,
                    RomFile = packageName,
                    EmulatorId = activityName
                };

                await PlayTimeHistoryFactory.Instance.Update(packageName, PlayState.Start);

                gameStarted = true;

                _appProvider.LaunchApp(tempGame);

                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"앱 실행에 실패했습니다: {ex.Message}", ex);
            }
            finally
            {
                if (gameStarted)
                {
                    var romPath = game.GetRomFullPath();
                    var fileContent = File.ReadAllText(romPath);
                    var packageName = fileContent.Split('|')[0];

                    await PlayTimeHistoryFactory.Instance.Update(packageName, PlayState.Stop);
                }
            }
        }

        private static Command GetEmulatorCommand(string platformId, string? emulatorId = null)
        {
            var service = (CommandConfigServiceFactory.Create?.Invoke()) ?? throw new InvalidOperationException("CommandConfigService가 초기화되지 않았습니다.");
            var config = service.LoadConfig();

            if (!string.IsNullOrEmpty(emulatorId))
            {
                if (config.Emulators.TryGetValue(emulatorId, out var specifiedEmulator))
                {
                    if (specifiedEmulator is Command specifiedCommand) return specifiedCommand;

                    throw new InvalidOperationException("잘못된 에뮬레이터 명령 타입입니다.");
                }
            }

            var normalizedPlatformId = PlatformInfoService.Instance.NormalizePlatformId(platformId);

            if (config.DefaultEmulators.TryGetValue(normalizedPlatformId, out string? defaultEmulatorId))
                if (!config.Emulators.ContainsKey(defaultEmulatorId)) defaultEmulatorId = null;

            if (string.IsNullOrEmpty(defaultEmulatorId))
            {
                var supportedEmulators = config.Emulators.Values
                    .Where(e => e.SupportedPlatforms
                    .Select(p => PlatformInfoService.Instance.NormalizePlatformId(p))
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

        private static void ValidateRomPath(string romPath)
        {
            if (string.IsNullOrWhiteSpace(romPath)) throw new ArgumentException("ROM 경로가 비어있습니다.", nameof(romPath));

            var file = new Java.IO.File(romPath);

            if (!file.Exists()) throw new FileNotFoundException($"ROM 파일을 찾을 수 없습니다: {romPath}");

            if (!file.CanRead()) throw new UnauthorizedAccessException($"ROM 파일을 읽을 수 없습니다: {romPath}");
        }

        private static MainActivity GetMainActivityInstance()
        {
            if (MainActivity.Instance == null) throw new InvalidOperationException("MainActivity 인스턴스를 사용할 수 없습니다.");

            return MainActivity.Instance;
        }

        private static async Task LaunchIntent(Intent intent)
        {
            var activity = GetMainActivityInstance();
            if (activity == null || activity.IsFinishing || activity.IsDestroyed) return;

            var tcs = new TaskCompletionSource<bool>();
            activity.SetGameExitWaiter(tcs);

            activity.Window?.SetFlags(WindowManagerFlags.Secure, WindowManagerFlags.Secure);
            activity.StartActivity(intent);
            activity.OverridePendingTransition(0, 0);

            await tcs.Task;
        }
    }
}