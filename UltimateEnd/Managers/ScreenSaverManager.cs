using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using UltimateEnd.Models;
using UltimateEnd.Orchestrators;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Managers
{
    public class ScreenSaverManager : IDisposable
    {
        #region Singleton

        private static ScreenSaverManager? _instance;
        public static ScreenSaverManager Instance => _instance ??= new();

        private ScreenSaverManager() { }

        #endregion

        #region Fields

        private IdleDetectionService? _idleDetectionService;
        private ScreensaverViewModel? _screensaverViewModel;
        private ViewModelBase? _previousView;
        private PlatformListViewModel? _platformListViewModel;
        private ViewModelBase? _currentView;
        private bool _isWindowActive = true;

        #endregion

        #region Events

        public event Action<ViewModelBase>? ViewChangeRequested;
        public event Func<Task>? BackToPlatformListRequested;
        public event Action? PreviousPlatformRequested;
        public event Action? NextPlatformRequested;
        public event Action<object?, string>? PlatformImageChangeRequested;
        public event Action<GameMetadata>? FavoritesChanged;
        public event Action<Platform>? LastSelectedPlatformChanged;

        #endregion

        #region Properties

        public bool IsScreensaverActive => _screensaverViewModel != null;

        #endregion

        #region Initialization

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

        #endregion

        #region Public Methods

        public void RegisterPlatformListViewModel(PlatformListViewModel viewModel) => _platformListViewModel = viewModel;

        public void SetTimeout(double minutes)
        {
            if (_idleDetectionService != null) _idleDetectionService.IdleTimeout = TimeSpan.FromMinutes(minutes);
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

        public void NotifyCurrentView(ViewModelBase? view)
        {
            _currentView = view;

            if (view != null && view != _screensaverViewModel) _idleDetectionService?.ResetIdleTimer();
        }

        public void OnWindowDeactivated()
        {
            _isWindowActive = false;

            if (_screensaverViewModel != null) RestoreFromScreensaver();

            _idleDetectionService?.Disable();
        }

        public void OnWindowActivated()
        {
            _isWindowActive = true;
            _idleDetectionService?.Enable();
            _idleDetectionService?.ResetIdleTimer();
        }

        #endregion

        #region Event Handlers

        private async void OnScreensaverActivated()
        {
            if (!_isWindowActive)
            {
                _idleDetectionService?.ResetIdleTimer();
                return;
            }

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

            bool hasVideoGames = await _screensaverViewModel?.InitializeAsync([.. _platformListViewModel?.Platforms]);

            if (!hasVideoGames)
            {
                CleanupScreensaver();
                _idleDetectionService?.ResetIdleTimer();
                return;
            }

            ViewChangeRequested?.Invoke(_screensaverViewModel);

            var window = GetMainWindow();
            window?.Activate();
        }

        private void OnUserActivityDetected()
        {
            if (_screensaverViewModel != null) RestoreFromScreensaver();
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
            await Task.Delay(100);

            var orchestrator = new GameLaunchOrchestrator(null);
            await orchestrator.LaunchAsync(game);

            var gameListViewModel = new GameListViewModel(platform, game)
            {
                ViewMode = SettingsService.LoadSettings().GameViewMode
            };

            gameListViewModel.BackRequested += async () =>
            {
                if (BackToPlatformListRequested != null) await BackToPlatformListRequested.Invoke();
            };
            gameListViewModel.FavoritesChanged += (s, g) => FavoritesChanged?.Invoke(g);
            gameListViewModel.PreviousPlatformRequested += () => PreviousPlatformRequested?.Invoke();
            gameListViewModel.NextPlatformRequested += () => NextPlatformRequested?.Invoke();
            gameListViewModel.PlatformImageChangeRequested += (s, e) => PlatformImageChangeRequested?.Invoke(s, e);

            LastSelectedPlatformChanged?.Invoke(platform);

            ViewChangeRequested?.Invoke(gameListViewModel);

            await Task.Delay(100);

            Dispatcher.UIThread.Post(() =>
            {
                if (GetCurrentView() == gameListViewModel && game != null)
                    gameListViewModel.ScrollToGame(game);
            }, DispatcherPriority.Loaded);

            Dispatcher.UIThread.Post(() =>
            {
                _idleDetectionService?.ResetIdleTimer();
                _idleDetectionService?.Enable();
            }, DispatcherPriority.Background);
        }

        #endregion

        #region Private Methods

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

        private ViewModelBase? GetCurrentView() => _currentView;

        private static Control? GetMainWindowContent()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) return desktop.MainWindow?.Content as Control;

            if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime single) return single.MainView as Control;

            return null;
        }

        private static Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) return desktop.MainWindow;

            return null;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _idleDetectionService?.Dispose();
            _screensaverViewModel?.Dispose();
            _instance = null;
        }

        #endregion
    }
}