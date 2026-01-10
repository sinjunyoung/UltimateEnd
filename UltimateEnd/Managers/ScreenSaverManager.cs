using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Managers
{
    public class ScreenSaverManager : IDisposable
    {
        private static ScreenSaverManager? _instance;

        public static ScreenSaverManager Instance => _instance ??= new();

        private IdleDetectionService? _idleDetectionService;
        private ScreensaverViewModel? _screensaverViewModel;
        private ViewModelBase? _previousView;
        private PlatformListViewModel? _platformListViewModel;
        private ViewModelBase? _currentView;

        public event Action<ViewModelBase>? ViewChangeRequested;
        public event Func<Task>? BackToPlatformListRequested;
        public event Action? PreviousPlatformRequested;
        public event Action? NextPlatformRequested;
        public event Action<object?, string>? PlatformImageChangeRequested;
        public event Action<GameMetadata>? FavoritesChanged;
        public event Action<Platform>? LastSelectedPlatformChanged;

        private ScreenSaverManager() { }

        public void Initialize(double timeoutMinutes)
        {
            _idleDetectionService = new IdleDetectionService
            {
                IdleTimeout = TimeSpan.FromMinutes(timeoutMinutes)
            };

            _idleDetectionService.ScreensaverActivated += OnScreensaverActivated;
            _idleDetectionService.UserActivityDetected += OnUserActivityDetected;
            _idleDetectionService.Start();
        }

        public void RegisterPlatformListViewModel(PlatformListViewModel viewModel) => _platformListViewModel = viewModel;

        public void SetTimeout(double minutes)
        {
            if (_idleDetectionService != null)
                _idleDetectionService.IdleTimeout = TimeSpan.FromMinutes(minutes);
        }

        public void PauseScreenSaver() => _idleDetectionService?.Disable();

        public void ResumeScreenSaver()
        {
            _idleDetectionService?.ResetIdleTimer();
            _idleDetectionService?.Enable();
        }

        public void ResetIdleTimer() => _idleDetectionService?.ResetIdleTimer();

        public void OnAppPaused()
        {
            _idleDetectionService?.Stop();
            _screensaverViewModel?.Pause();
        }

        public void OnAppResumed() => ResumeScreenSaver();

        public bool IsScreensaverActive => _screensaverViewModel != null;

        private async void OnScreensaverActivated()
        {
            if (_screensaverViewModel != null) return;

            var root = GetMainWindowContent();
            if (root != null && OverlayHelper.IsAnyOverlayVisible(root))
            {
                _idleDetectionService?.ResetIdleTimer();
                return;
            }

            _previousView = GetCurrentView();

            if (_previousView is GameListViewModel gameListVM)
            {
                gameListVM.StopVideo();
                await Task.Delay(100);
            }

            _screensaverViewModel = new ScreensaverViewModel();
            _screensaverViewModel.NavigateToGame += OnScreensaverNavigateToGame;
            _screensaverViewModel.ExitScreensaver += OnScreensaverExit;

            ViewChangeRequested?.Invoke(_screensaverViewModel);

            var window = GetMainWindow();
            window?.Activate();

            if (_platformListViewModel != null)
                await _screensaverViewModel.InitializeAsync([.. _platformListViewModel.Platforms]);
        }

        private static Control? GetMainWindowContent()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow?.Content as Control;

            if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single)
                return single.MainView as Control;

            return null;
        }

        private static Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;

            return null;
        }

        private void OnUserActivityDetected()
        {
            if (_screensaverViewModel != null)
                RestoreFromScreensaver();
        }

        private void OnScreensaverExit() => RestoreFromScreensaver();

        private async void OnScreensaverNavigateToGame()
        {
            var (platform, game) = _screensaverViewModel?.GetCurrentSelection() ?? (null, null);

            if (platform == null || game == null)
            {
                RestoreFromScreensaver();
                return;
            }

            _screensaverViewModel?.Pause();
            _idleDetectionService?.Disable();
            CleanupScreensaver();

            ViewChangeRequested?.Invoke(null);
            await Task.Delay(300);

            var gameListViewModel = new GameListViewModel(platform)
            {
                ViewMode = SettingsService.LoadSettings().GameViewMode
            };

            gameListViewModel.BackRequested += async () =>
            {
                if (BackToPlatformListRequested != null)
                    await BackToPlatformListRequested.Invoke();
            };
            gameListViewModel.FavoritesChanged += (s, g) => FavoritesChanged?.Invoke(g);
            gameListViewModel.PreviousPlatformRequested += () => PreviousPlatformRequested?.Invoke();
            gameListViewModel.NextPlatformRequested += () => NextPlatformRequested?.Invoke();
            gameListViewModel.PlatformImageChangeRequested += (s, e) => PlatformImageChangeRequested?.Invoke(s, e);

            LastSelectedPlatformChanged?.Invoke(platform);

            var targetGame = gameListViewModel.Games.FirstOrDefault(g => g.RomFile == game.RomFile);
            gameListViewModel.SelectedGame = targetGame;

            if (targetGame != null)
                await gameListViewModel.LaunchGameAsync(targetGame);

            ViewChangeRequested?.Invoke(gameListViewModel);

            Dispatcher.UIThread.Post(async () =>
            {
                await Task.Delay(500);

                if (GetCurrentView() == gameListViewModel && targetGame != null)
                {
                    gameListViewModel.ScrollToGame(targetGame);
                    await Task.Delay(400);

                    if (OperatingSystem.IsWindows())
                    {
                        gameListViewModel.StopVideo();
                        await Task.Delay(50);
                        await gameListViewModel.ResumeVideoAsync();
                    }
                }

                if (OperatingSystem.IsWindows())
                {
                    _idleDetectionService?.ResetIdleTimer();
                    _idleDetectionService?.Enable();
                }
            }, DispatcherPriority.Loaded);
        }

        private async void RestoreFromScreensaver()
        {
            var previousGameListVM = _previousView as GameListViewModel;

            if (_previousView != null)
            {
                ViewChangeRequested?.Invoke(_previousView);
                _previousView = null;
            }
            else if (_platformListViewModel != null)
                ViewChangeRequested?.Invoke(_platformListViewModel);

            CleanupScreensaver();

            if (previousGameListVM != null)
            {
                await Task.Delay(100);
                previousGameListVM.StopVideo();
                await Task.Delay(50);
                await previousGameListVM.ResumeVideoAsync();
            }
        }

        private void CleanupScreensaver()
        {
            if (_screensaverViewModel != null)
            {
                _screensaverViewModel.NavigateToGame -= OnScreensaverNavigateToGame;
                _screensaverViewModel.ExitScreensaver -= OnScreensaverExit;
                _screensaverViewModel.Dispose();
                _screensaverViewModel = null;
            }
        }

        public void NotifyCurrentView(ViewModelBase? view)
        {
            _currentView = view;

            if (view != null && view != _screensaverViewModel)
                _idleDetectionService?.ResetIdleTimer();
        }

        private ViewModelBase? GetCurrentView() => _currentView;

        public void Dispose()
        {
            _idleDetectionService?.Dispose();
            _screensaverViewModel?.Dispose();
            _instance = null;
        }
    }
}