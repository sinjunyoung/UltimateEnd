using ReactiveUI;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Scraper;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private ViewModelBase _currentView;
        private PlatformListViewModel? _platformListViewModel;
        private Platform? _lastSelectedPlatform;
        private CancellationTokenSource? _platformSwitchCts;
        private const int PlatformSwitchDelayMs = 300;

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set
            {
                this.RaiseAndSetIfChanged(ref _currentView, value);
                ScreenSaverManager.Instance.NotifyCurrentView(value);
            }
        }

        public MainViewModel()
        {
            var provider = AssetPathProviderFactory.Create?.Invoke();

            if (provider != null) FbNeoGameDatabase.Initialize(provider);

            var settings = SettingsService.LoadSettings();

            if (settings.PlatformSettings != null && settings.PlatformSettings.Count > 0)
            {
                var platformKeys = settings.PlatformSettings.Keys.ToList();

                _ = Task.Run(() =>
                {
                    MetadataService.PreloadHasGamesCache(platformKeys);
                    AllGamesManager.Instance.GetAllGames();
                });
            }

            if (settings.RomsBasePaths.Count == 0) NavigateToRomSettings();
            else
            {
                InitializePlatformListVM();
                CurrentView = _platformListViewModel!;
            }

            ScreenSaverManager.Instance.Initialize(settings.ScreensaverTimeoutMinutes);
            ScreenSaverManager.Instance.ViewChangeRequested += OnViewChangeRequested;
            ScreenSaverManager.Instance.BackToPlatformListRequested += OnBackToPlatformListAsync;
            ScreenSaverManager.Instance.PreviousPlatformRequested += OnPreviousPlatformRequested;
            ScreenSaverManager.Instance.NextPlatformRequested += OnNextPlatformRequested;
            ScreenSaverManager.Instance.PlatformImageChangeRequested += OnPlatformImageChangeRequested;
            ScreenSaverManager.Instance.FavoritesChanged += game => _platformListViewModel?.UpdateFavorites();
            ScreenSaverManager.Instance.LastSelectedPlatformChanged += platform => _lastSelectedPlatform = platform;

            if (_platformListViewModel != null)
                ScreenSaverManager.Instance.RegisterPlatformListViewModel(_platformListViewModel);
        }

        private void OnViewChangeRequested(ViewModelBase? newView)
        {
            CurrentView = newView;

            if (newView is GameListViewModel gameListViewModel && ScreenScraperConfig.Instance.EnableAutoScrap) AutoScrapService.Instance.Start(gameListViewModel.Games);
        }

        private void InitializePlatformListVM()
        {
            if (_platformListViewModel == null)
            {
                _platformListViewModel = new PlatformListViewModel();

                _platformListViewModel.PlatformSelected += OnPlatformSelected;
                _platformListViewModel.EmulatorSettingViewRequested += OnEmulatorSettingViewRequested;
                _platformListViewModel.ScreensaverTimeoutChanged += (s, e) => ScreenSaverManager.Instance.SetTimeout(e);
                _platformListViewModel.KeyBindingSettingViewRequested += OnKeyBindingSettingViewRequested;

                ScreenSaverManager.Instance.RegisterPlatformListViewModel(_platformListViewModel);
            }
        }

        public void NavigateToRomSettings()
        {
            var romSettingViewModel = new RomSettingViewModel();

            romSettingViewModel.BackRequested += OnBackToPlatformList;
            CurrentView = romSettingViewModel;
        }

        public void NavigateToSettings()
        {
            var settingsViewModel = new SettingsViewModel();

            settingsViewModel.BackRequested += OnBackToPlatformList;
            CurrentView = settingsViewModel;
        }

        private void OnPlatformSelected(Platform platform)
        {
            _lastSelectedPlatform = platform;

            var gameListViewModel = new GameListViewModel(platform)
            {
                ViewMode = SettingsService.LoadSettings().GameViewMode
            };

            gameListViewModel.BackRequested += OnBackToPlatformList;
            gameListViewModel.FavoritesChanged += (s, game) => _platformListViewModel.UpdateFavorites();
            gameListViewModel.PreviousPlatformRequested += OnPreviousPlatformRequested;
            gameListViewModel.NextPlatformRequested += OnNextPlatformRequested;
            gameListViewModel.PlatformImageChangeRequested += OnPlatformImageChangeRequested;

            CurrentView = gameListViewModel;

            if (ScreenScraperConfig.Instance.EnableAutoScrap) AutoScrapService.Instance.Start(gameListViewModel.Games);
        }

        private async void OnBackToPlatformList() => await OnBackToPlatformListAsync();

        private async Task OnBackToPlatformListAsync()
        {
            var gameListVM = CurrentView as GameListViewModel;

            if (_platformListViewModel == null) InitializePlatformListVM();

            if (CurrentView is not RomSettingViewModel)
                if (NeedsRefresh()) await _platformListViewModel.LoadPlatformsAsync();

            if (_lastSelectedPlatform != null)
            {
                var platformToRestore = _platformListViewModel!.Platforms
                    .FirstOrDefault(p => p.Id == _lastSelectedPlatform.Id);

                if (platformToRestore != null)
                {
                    var index = _platformListViewModel.Platforms.IndexOf(platformToRestore);

                    if (index >= 0) _platformListViewModel.SelectedIndex = index;
                }
            }

            CurrentView = _platformListViewModel!;

            await Task.Delay(50);
            _platformListViewModel.TriggerScrollFix = !_platformListViewModel.TriggerScrollFix;

            if (gameListVM != null)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    try { gameListVM.Dispose(); }
                    catch { }
                });
            }
        }

        private bool NeedsRefresh()
        {
            var hasFavorites = FavoritesManager.Count > 0;
            var hasHistory = AllGamesManager.GetHistoryCount() > 0;

            var currentHasFavorites = _platformListViewModel?.Platforms
                .Any(p => p.Id == GameMetadataManager.FavoritesKey) ?? false;
            var currentHasHistory = _platformListViewModel?.Platforms
                .Any(p => p.Id == GameMetadataManager.HistoriesKey) ?? false;

            return hasFavorites != currentHasFavorites || hasHistory != currentHasHistory;
        }

        private void OnEmulatorSettingViewRequested(object? sender, EventArgs e)
        {
            EmulatorSettingViewModelBase viewModel = EmulatorSettingViewFactory.Create?.Invoke();

            viewModel.BackRequested += OnBackFromEmulatorSetting;
            CurrentView = viewModel;
        }

        private void OnKeyBindingSettingViewRequested(object? sender, EventArgs e)
        {
            KeyBindingSettingsViewModelBase viewModel = KeyBindingSettingsViewFactory.Create?.Invoke();

            viewModel.BackRequested += OnBackFromEmulatorSetting;
            CurrentView = viewModel;
        }

        private void OnBackFromEmulatorSetting()
        {
            InitializePlatformListVM();
            CurrentView = _platformListViewModel!;
        }

        private void OnPreviousPlatformRequested()
        {
            if (_platformListViewModel == null) return;

            _platformSwitchCts?.Cancel();
            _platformSwitchCts = new CancellationTokenSource();
            var cts = _platformSwitchCts;

            var oldGameListVM = CurrentView as GameListViewModel;

            _platformListViewModel.MoveLeft();
            var newPlatform = _platformListViewModel.SelectedPlatform;
            _lastSelectedPlatform = newPlatform;

            oldGameListVM?.StopVideo();

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(PlatformSwitchDelayMs, cts.Token);

                    if (!cts.Token.IsCancellationRequested)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            OnPlatformSelected(newPlatform);

                            if (oldGameListVM != null)
                            {
                                Task.Run(async () =>
                                {
                                    await Task.Delay(500);
                                    try { oldGameListVM.Dispose(); }
                                    catch { }
                                });
                            }
                        });
                    }
                }
                catch (TaskCanceledException) { }
            });
        }

        private void OnNextPlatformRequested()
        {
            if (_platformListViewModel == null) return;

            _platformSwitchCts?.Cancel();
            _platformSwitchCts = new CancellationTokenSource();
            var cts = _platformSwitchCts;

            var oldGameListVM = CurrentView as GameListViewModel;

            _platformListViewModel.MoveRight();
            var newPlatform = _platformListViewModel.SelectedPlatform;
            _lastSelectedPlatform = newPlatform;

            oldGameListVM?.StopVideo();

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(PlatformSwitchDelayMs, cts.Token);

                    if (!cts.Token.IsCancellationRequested)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            OnPlatformSelected(newPlatform);

                            if (oldGameListVM != null)
                            {
                                Task.Run(async () =>
                                {
                                    await Task.Delay(500);
                                    try { oldGameListVM.Dispose(); }
                                    catch { }
                                });
                            }
                        });
                    }
                }
                catch (TaskCanceledException) { }
            });
        }


        private void OnPlatformImageChangeRequested(object? sender, string e)
        {
            if (_lastSelectedPlatform != null)
            {
                _lastSelectedPlatform.ImagePath = e;

                var actualPlatform = _platformListViewModel?.Platforms
                    .FirstOrDefault(p => p.Id == _lastSelectedPlatform.Id);

                if (actualPlatform != null && actualPlatform != _lastSelectedPlatform) actualPlatform.ImagePath = e;

                SavePlatforms();
            }
        }

        public void SavePlatforms()
        {
            var settings = SettingsService.LoadSettings();

            if (_lastSelectedPlatform != null)
            {
                if (string.IsNullOrEmpty(_lastSelectedPlatform.ImagePath))
                {
                    settings.PlatformImages.Remove(_lastSelectedPlatform.Id);
                    _lastSelectedPlatform.ImagePath = ResourceHelper.GetPlatformImage(_lastSelectedPlatform.Id);
                }
                else settings.PlatformImages[_lastSelectedPlatform.Id] = _lastSelectedPlatform.ImagePath;

                SettingsService.SavePlatformSettings(settings);
            }
        }

        public void RefreshCurrentView()
        {
            var temp = _currentView;
            _currentView = null;
            this.RaisePropertyChanged(nameof(CurrentView));
            _currentView = temp;
            this.RaisePropertyChanged(nameof(CurrentView));
        }

        public static void OnUserInteractionDetected()
        {
            if (!ScreenSaverManager.Instance.IsScreensaverActive) ScreenSaverManager.Instance.ResetIdleTimer();
        }

        public void Dispose()
        {
            _platformSwitchCts?.Cancel();

            ScreenSaverManager.Instance.ViewChangeRequested -= OnViewChangeRequested;
            ScreenSaverManager.Instance.BackToPlatformListRequested -= OnBackToPlatformListAsync;
            ScreenSaverManager.Instance.PreviousPlatformRequested -= OnPreviousPlatformRequested;
            ScreenSaverManager.Instance.NextPlatformRequested -= OnNextPlatformRequested;
            ScreenSaverManager.Instance.PlatformImageChangeRequested -= OnPlatformImageChangeRequested;
            ScreenSaverManager.Instance.LastSelectedPlatformChanged -= platform => _lastSelectedPlatform = platform;

            ScreenSaverManager.Instance.Dispose();
        }
    }
}