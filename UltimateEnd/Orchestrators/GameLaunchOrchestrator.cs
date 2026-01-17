using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Threading.Tasks;
using UltimateEnd.Coordinators;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Orchestrators
{
    public class GameLaunchOrchestrator(VideoPlaybackCoordinator videoCoordinator)
    {
        private const int LaunchDelayMs = 50;
        private readonly VideoPlaybackCoordinator _videoCoordinator = videoCoordinator;

        public event Func<Task>? AppActivated;
        public event Action<bool>? VideoContainerVisibilityRequested;
        public event Action? LaunchCompleted;
        public event Action? LaunchFailed;
        public event Action<bool>? IdleDetectionEnabled;

        public async Task LaunchAsync(GameMetadata game)
        {
            PrepareForLaunch();
            IdleDetectionEnabled?.Invoke(false);

            if (!OperatingSystem.IsAndroid()) ScreenSaverManager.Instance.PauseScreenSaver();

            try
            {
                var launcher = (GameLauncherFactory.Create?.Invoke()) ?? throw new InvalidOperationException("GameLauncher를 생성할 수 없습니다.");
                var validation = await launcher.ValidateEmulatorAsync(game);

                if (!validation.IsValid)
                {
                    var action = await HandleValidationFailure(validation);

                    if (action == EmulatorValidationAction.Retry)
                    {
                        validation = await launcher.ValidateEmulatorAsync(game);
                        if (!validation.IsValid)
                        {
                            LaunchFailed?.Invoke();
                            IdleDetectionEnabled?.Invoke(true);
                            return;
                        }
                    }
                    else
                    {
                        LaunchFailed?.Invoke();
                        IdleDetectionEnabled?.Invoke(true);
                        return;
                    }
                }

                await DeactivateApp();
                await launcher.LaunchGameAsync(game);
            }
            catch (Exception ex)
            {
                await HandleError(ex);
                LaunchFailed?.Invoke();
                IdleDetectionEnabled?.Invoke(true);
                return;
            }

            ActivateApp();
            game.RefreshPlayHistory();
            await Task.Delay(100);

            IdleDetectionEnabled?.Invoke(true);
            LaunchCompleted?.Invoke();

            if (!OperatingSystem.IsAndroid()) ScreenSaverManager.Instance.ResumeScreenSaver();
        }

        private async Task<EmulatorValidationAction> HandleValidationFailure(EmulatorValidationResult validation)
        {
            ActivateApp();
            await Task.Delay(100);

            VideoContainerVisibilityRequested?.Invoke(false);

            var handler = EmulatorValidationHandlerFactory.Create?.Invoke();
            var action = handler != null ? await handler.HandleValidationFailedAsync(validation) : EmulatorValidationAction.Cancel;

            VideoContainerVisibilityRequested?.Invoke(true);

            return action;
        }

        private void PrepareForLaunch()
        {
            _videoCoordinator?.CancelDelay();
            _videoCoordinator?.Stop();
            //Task.Delay(LaunchDelayMs).Wait();
        }

        private async Task HandleError(Exception ex)
        {
            ActivateApp();
            await Task.Delay(200);

            VideoContainerVisibilityRequested?.Invoke(false);

            await DialogService.Instance.ShowMessage("게임 실행 오류", $"게임 실행 중 오류가 발생했습니다:\n{ex.Message}", MessageType.Error);

            VideoContainerVisibilityRequested?.Invoke(true);
        }

        private async static Task DeactivateApp()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow?.Hide();
                await Task.Delay(100);
            }
        }

        private void ActivateApp()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;

                if (mainWindow != null)
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                    AppActivated?.Invoke();
                }
            }
        }
    }
}