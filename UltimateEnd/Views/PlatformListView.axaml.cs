using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using UltimateEnd.Views.Managers;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public partial class PlatformListView : UserControl
    {
        #region Constants
        
        private const int MAX_INITIALIZATION_ATTEMPTS = 20;
        private const int INITIALIZATION_DELAY_MS = 5;
        private const int FADE_OUT_FRAME_DELAY = 16;
        
        #endregion

        #region Fields
        
        private CarouselManager? _carouselManager;
        private PlatformDragDropManager? _dragDropManager;
        private bool _isInitialized = false;
        private int _currentMenuIconIndex = 0;
        private PathIcon[] _menuIcons = [];
        private bool _isShuttingDown = false;

        #endregion

        #region Properties

        private PlatformListViewModel? ViewModel => DataContext as PlatformListViewModel;

        #endregion

        public PlatformListView()
        {
            InitializeComponent();

            Focusable = true;
            Background = Brushes.Transparent;
            AttachedToVisualTree += OnAttachedToVisualTree;
            DataContextChanged += OnDataContextChanged;
        }

        #region Lifecycle

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            Focus();
            SetupEventHandlers();
            SetupOverlays();
            Dispatcher.UIThread.Post(InitializePosition, DispatcherPriority.Loaded);

            Bitmap oldImage = BackgroundImage.Source as Bitmap;
            string backgroundImagePath = SettingsService.LoadSettings()?.BackgroundImagePath;

            if (!string.IsNullOrEmpty(backgroundImagePath) && File.Exists(backgroundImagePath))
                BackgroundImage.Source = new Bitmap(backgroundImagePath);
            else
                BackgroundImage.Source = ResourceHelper.LoadResourceImage("background");

            oldImage?.Dispose();
        }

        private async void InitializePosition()
        {
            if (PlatformItemsControl == null || CarouselContainer == null) return;

            await WaitForContainerReady();

            _carouselManager = new CarouselManager(PlatformItemsControl, CarouselContainer, CarouselScrollViewer, this);
            _dragDropManager = new PlatformDragDropManager(this, _carouselManager);
            _dragDropManager.Setup(this);

            _isInitialized = true;

            var borders = await WaitForCardsReady();

            if (borders.Count > 0)
                _carouselManager.UpdateCardStylesAndScroll(ViewModel!);
            
            PlatformItemsControl.Opacity = 1.0;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            AttachedToVisualTree -= OnAttachedToVisualTree;
            ThemeService.ThemeChanged -= OnThemeChangedHandler;

            if (ViewModel != null) ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            _carouselManager?.ClearCache();

            base.OnDetachedFromVisualTree(e);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (ViewModel != null) ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            if (ViewModel != null)
            {
                ViewModel.IsMenuFocused = false;

                ViewModel.PropertyChanged += OnViewModelPropertyChanged;
                ThemeService.ThemeChanged += OnThemeChangedHandler;
                ViewModel.Platforms.CollectionChanged += OnPlatformsCollectionChanged;
            }
        }

        private void OnPlatformsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_carouselManager != null && ViewModel != null)
                {
                    _carouselManager.ClearCache();
                    PlatformItemsControl.InvalidateMeasure();
                    _carouselManager.UpdateCardStylesAndScroll(ViewModel);
                }
            }, DispatcherPriority.Loaded);
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_dragDropManager?.IsDragging ?? false) return;

            if (e.PropertyName == nameof(PlatformListViewModel.SelectedIndex) && _isInitialized && ViewModel != null && !ViewModel.IsMenuFocused)
                _carouselManager?.UpdateCardStylesAndScroll(ViewModel);

            if (e.PropertyName == nameof(PlatformListViewModel.TriggerScrollFix))
                if (PlatformItemsControl != null) Dispatcher.UIThread.Post(() => PlatformItemsControl.InvalidateMeasure(), DispatcherPriority.Background);
        }

        private void OnThemeChangedHandler(string theme) => _carouselManager?.ClearCache();

        #endregion

        #region Initialization

        private void SetupEventHandlers()
        {
            if (CarouselContainer != null)
            {
                CarouselContainer.PropertyChanged += (s, args) =>
                {
                    if (_dragDropManager?.IsDragging ?? false) return;

                    if (args.Property.Name == nameof(Border.Bounds) && _isInitialized && ViewModel != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"CarouselContainer Bounds 변경: {CarouselContainer.Bounds.Width}x{CarouselContainer.Bounds.Height}");

                        _carouselManager?.ClearCache();
                        _carouselManager?.UpdateCardStylesAndScroll(ViewModel);
                    }
                };
            }

            AttachMenuIconHandlers();
            InitializeMenuIcons();
            ResetMenuIcons();
        }

        private void SetupOverlays()
        {
            SettingsOverlay.Hidden += async (s, e) =>
            {
                if (e.State == HiddenState.Close)
                    await WavSounds.Cancel();
                else if (e.State == HiddenState.Cancel)
                    await WavSounds.Cancel();
                else if (e.State == HiddenState.Confirm)
                    await WavSounds.OK();

                Dispatcher.UIThread.Post(() =>
                {
                    if (ViewModel != null && !ViewModel.IsMenuFocused)
                    {
                        this.Focus();
                        _carouselManager?.UpdateCardStylesAndScroll(ViewModel);
                    }
                }, DispatcherPriority.Input);
            };

            SettingsOverlay.Click += async (s, e) => await WavSounds.Click();

            BaseOverlay[] subOverlays = [PlaylistManagementOverlay, ScrapOverlay, ThemeOverlay, GenreEditorOverlay ];

            foreach (var overlay in subOverlays)
            {
                overlay.Hidden += async (s, e) =>
                {
                    if (e.State == HiddenState.Close)
                        await WavSounds.Cancel();
                    else if (e.State == HiddenState.Cancel)
                        await WavSounds.Cancel();
                    else if (e.State == HiddenState.Confirm)
                        await WavSounds.OK();

                    SettingsOverlay.Focus();
                };

                overlay.Click += async (s, e) => await WavSounds.Click();
            }

            SettingsOverlay.ThemeClicked += (s, e) => ThemeOverlay.Show();
            SettingsOverlay.BackgroundImageClicked += async (s, e) =>
            {
                var converter = PathConverterFactory.Create?.Invoke();
                string backgroundImagePath = await DialogHelper.OpenFileAsync(null, FilePickerFileTypes.ImageAll);

                Bitmap oldImage = BackgroundImage.Source as Bitmap;

                if (!string.IsNullOrEmpty(backgroundImagePath))
                    BackgroundImage.Source = new Bitmap(backgroundImagePath);
                else
                    BackgroundImage.Source = ResourceHelper.LoadResourceImage("background");

                oldImage?.Dispose();

                var setting = SettingsService.LoadSettings();
                setting.BackgroundImagePath = backgroundImagePath;
                SettingsService.SaveSettingsQuiet(setting);
            };

            SettingsOverlay.EmulatorClicked += (s, e) => ViewModel.OnShowEmulatorSettingViewRequested();
            SettingsOverlay.PlaylistClicked += (s, e) => PlaylistManagementOverlay.Show();
            SettingsOverlay.ScreensaverTimeoutChanged += (s, e) => ViewModel.OnScreensaverTimeoutChangedRequested(e);
            SettingsOverlay.ScrapClicked += (s, e) => ScrapOverlay.Show();
            SettingsOverlay.KeyBindingClicked += (s, e) => ViewModel.OnShowKeyBindingSettingViewRequested();
            SettingsOverlay.GenreClicked += (s, e) => GenreEditorOverlay.Show();

            PlaylistManagementOverlay.PlaylistsNameChanged += (s, e) =>
            {
                var platformId = PlaylistManager.GetPlaylistPlatformId(e.playlistId);
                var platform = ViewModel.Platforms.FirstOrDefault(p => p.Id == platformId);

                if (platform != null) platform.Name = e.name;
            };
            PlaylistManagementOverlay.PlaylistCoverChanged += (s, e) =>
            {
                if (ViewModel == null) return;

                var (playlistId, coverImagePath) = e;
                var platformId = PlaylistManager.GetPlaylistPlatformId(playlistId);

                var platform = ViewModel.Platforms.FirstOrDefault(p => p.Id == platformId);

                if (platform != null)
                {
                    var coverImage = string.IsNullOrEmpty(coverImagePath) || !File.Exists(coverImagePath) ? ResourceHelper.GetPlatformImage("playlist") : coverImagePath;
                    platform.ImagePath = coverImage;
                }
            };
            PlaylistManagementOverlay.PlaylistsCreated += (s, e) => ViewModel.Platforms.Add(PlaylistManager.Instance.GetPlaylist(e)?.ToPlatform());
            PlaylistManagementOverlay.PlaylistsDeleted += (s, e) =>
            {
                var platformId = PlaylistManager.GetPlaylistPlatformId(e);
                var platform = ViewModel.Platforms.FirstOrDefault(p => p.Id == platformId);

                ViewModel.Platforms.Remove(platform);
            };

            ThemeOverlay.ThemeSelected += (s, theme) => ShowThemeLoadingAndApply(theme);
        }

        private async void ShowThemeLoadingAndApply(ThemeOption theme)
        {
            ThemeLoadingOverlay.IsVisible = true;

            await Task.Delay(100);
            await Task.Run(() => Dispatcher.UIThread.Invoke(() => PlatformListViewModel.SelectTheme(theme)));
            await Task.Delay(150);

            ThemeLoadingOverlay.IsVisible = false;
            SettingsOverlay.Hide(HiddenState.Silent);
        }

        private async Task WaitForContainerReady()
        {
            int attempts = 0;

            while (CarouselContainer!.Bounds.Width <= 0 && attempts < MAX_INITIALIZATION_ATTEMPTS)
            {
                await Task.Delay(INITIALIZATION_DELAY_MS);
                attempts++;
            }
        }

        private async Task<List<Border>> WaitForCardsReady()
        {
            var borders = _carouselManager?.GetPlatformCards() ?? [];
            int attempts = 0;

            while (borders.Count == 0 && attempts < MAX_INITIALIZATION_ATTEMPTS)
            {
                await Task.Delay(INITIALIZATION_DELAY_MS);

                borders = _carouselManager?.GetPlatformCards() ?? [];
                attempts++;
            }

            return borders;
        }

        #endregion

        #region Card Management

        private async void OnCardTapped(object? sender, TappedEventArgs e)
        {
            await WavSounds.OK();

            if (sender is not Border border || border.DataContext is not Platform platform) return;

            if (ViewModel == null) return;

            if (ViewModel.IsMenuFocused)
            {
                ViewModel.IsMenuFocused = false;
                ResetMenuIcons();
            }

            var index = ViewModel.Platforms.IndexOf(platform);

            if (index >= 0)
            {
                ViewModel.SelectedIndex = index;
                ViewModel.SelectCurrentPlatform();
            }
        }

        #endregion

        #region Menu Icon Management

        private void InitializeMenuIcons()
        {
            if (RandomGameIcon != null && SettingsIcon != null && FolderIcon != null && ShutdownIcon != null)
                _menuIcons = [RandomGameIcon, SettingsIcon, FolderIcon, ShutdownIcon];
        }

        private void AttachMenuIconHandlers()
        {
            Dispatcher.UIThread.Post(() =>
            {
                AttachMenuIconHandler("RandomGameIcon", OnRandomGameClick);
                AttachMenuIconHandler("FolderIcon", OnFolderIconClick);
                AttachMenuIconHandler("ShutdownIcon", OnShutdownIconClick);
            }, DispatcherPriority.Loaded);
        }

        private void AttachMenuIconHandler(string iconName, EventHandler<PointerPressedEventArgs> handler)
        {
            var icon = this.FindControl<PathIcon>(iconName);

            if (icon?.Parent is Border border) border.PointerPressed += handler;
        }

        private void FocusMenuIcon(int index)
        {
            if (_menuIcons.Length == 0) InitializeMenuIcons();

            if (index < 0 || index >= _menuIcons.Length) return;

            _currentMenuIconIndex = index;

            foreach (var icon in _menuIcons) icon.Classes.Remove("MenuFocused");

            _menuIcons[_currentMenuIconIndex].Classes.Add("MenuFocused");
        }

        private void ResetMenuIcons()
        {
            foreach (var icon in _menuIcons) icon.Classes.Remove("MenuFocused");
        }

        private void MoveToPreviousMenuIcon()
        {
            if (_menuIcons.Length == 0) return;

            _currentMenuIconIndex = (_currentMenuIconIndex - 1 + _menuIcons.Length) % _menuIcons.Length;
            FocusMenuIcon(_currentMenuIconIndex);
        }

        private void MoveToNextMenuIcon()
        {
            if (_menuIcons.Length == 0) return;

            _currentMenuIconIndex = (_currentMenuIconIndex + 1) % _menuIcons.Length;
            FocusMenuIcon(_currentMenuIconIndex);
        }

        #endregion

        #region Menu Actions

        private async void OnFolderIconClick(object? sender, PointerPressedEventArgs e)
        {
            if(e != null) e.Handled = true;

            await WavSounds.OK();
            NavigateToView(vm => vm.NavigateToRomSettings());
        }

        private async void OnShutdownIconClick(object? sender, PointerPressedEventArgs e)
        {
            if (!_isShuttingDown)
            {
                _isShuttingDown = true;
                e.Handled = true;

                await ShutdownApplication();
            }
        }

        private async void OnRandomGameClick(object? sender, PointerPressedEventArgs e)
        {
            if (e != null) e.Handled = true;

            await OnRandomGameClickAsync();

            Dispatcher.UIThread.Post(() =>
            {
                this.Focus();
                if (ViewModel != null && ViewModel.IsMenuFocused)
                {
                    FocusMenuIcon(_currentMenuIconIndex);
                }
            }, DispatcherPriority.Input);
        }

        private async void OnSettingsClick(object? sender, PointerPressedEventArgs e)
        {
            if (e != null) e.Handled = true;

            await WavSounds.OK();
            SettingsOverlay.Show();
        }

        private async void ExecuteCurrentMenuIcon()
        {
            if (_menuIcons.Length == 0) return;

            if (_currentMenuIconIndex == 0)
            {
                await OnRandomGameClickAsync();

                Dispatcher.UIThread.Post(() =>
                {
                    this.Focus();
                    if (ViewModel != null)
                    {
                        ViewModel.IsMenuFocused = true;
                        FocusMenuIcon(_currentMenuIconIndex);
                    }
                }, DispatcherPriority.Input);
                return;
            }

            if (ViewModel != null) ViewModel.IsMenuFocused = false;
            ResetMenuIcons();

            switch (_currentMenuIconIndex)
            {
                case 1: OnSettingsClick(null, null); break;
                case 2: OnFolderIconClick(null, null); break;
                case 3: await ShutdownApplication(); break;
            }
        }

        private async Task OnRandomGameClickAsync()
        {
            await WavSounds.Coin();

            if (ViewModel != null)
                await ViewModel.LaunchRandomGame();
        }

        private void NavigateToView(Action<MainViewModel> navigationAction)
        {
            var mainContentView = this.FindAncestorOfType<MainContentView>();

            if (mainContentView?.DataContext is MainViewModel mainVM) navigationAction(mainVM);
        }

        private async Task ShutdownApplication()
        {
            int duration = WavSounds.Durations.AppClosing;

            var soundTask = Task.Run(async () =>
            {
                await WavSounds.AppClosing();
                await Task.Delay(duration);
            });

            if (this.VisualRoot is TopLevel topLevel) await FadeOut(topLevel, duration);

            await soundTask;

            AppLifetimeFactory.Create?.Invoke()?.Shutdown();
        }

        private static async Task FadeOut(TopLevel topLevel, int duration)
        {
            var startTime = DateTime.Now;

            while (true)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

                if (elapsed >= duration) break;

                double progress = elapsed / duration;
                double eased = 1 - Math.Pow(1 - progress, 3);
                topLevel.Opacity = 1.0 - eased;

                await Task.Delay(FADE_OUT_FRAME_DELAY);
            }

            topLevel.Opacity = 0;
        }

        #endregion

        #region Keyboard Input

        protected async override void OnKeyDown(KeyEventArgs e)
        {
            await KeySoundHelper.PlaySoundForKeyEvent(e);

            base.OnKeyDown(e);

            if (ViewModel == null) return;

            var handled = ViewModel.IsMenuFocused ? HandleMenuNavigation(e) : HandleGridNavigation(e);

            if (handled) e.Handled = true;
        }

        private bool HandleMenuNavigation(KeyEventArgs e)
        {
            if (ViewModel == null) return false;

            if (InputManager.IsButtonPressed(e, GamepadButton.Select))
            {
                OnSettingsClick(null, null);
                return true;
            }
            if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
            {
                ViewModel.IsMenuFocused = false;
                ResetMenuIcons();
                _carouselManager?.UpdateCardStylesAndScroll(ViewModel);
                return true;
            }
            if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonB) || e.Key == Key.Escape)
            {
                ViewModel.IsMenuFocused = false;
                ResetMenuIcons();
                _carouselManager?.UpdateCardStylesAndScroll(ViewModel);
                return true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
            {
                MoveToPreviousMenuIcon();
                return true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
            {
                MoveToNextMenuIcon();
                return true;
            }
            else if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA))
            {
                ExecuteCurrentMenuIcon();
                return true;
            }
            return false;
        }

        private bool HandleGridNavigation(KeyEventArgs e)
        {
            if (ViewModel == null) return false;

            if (InputManager.IsButtonPressed(e, GamepadButton.Select))
            {
                OnSettingsClick(null, null);
                return true;
            }
            if (InputManager.IsButtonPressed(e, GamepadButton.DPadUp))
            {
                MoveGridUp();
                return true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
            {
                MoveGridDown();
                return true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
            {
                ViewModel.MoveLeft();
                return true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
            {
                ViewModel.MoveRight();
                return true;
            }
            else if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA))
            {
                _ = WavSounds.OK();
                ViewModel.SelectCurrentPlatform();
                return true;
            }

            return false;
        }

        private void MoveGridUp()
        {
            if (ViewModel == null) return;

            int columnsPerRow = _carouselManager?.CalculateColumnsPerRow() ?? 3;
            int newIndex = ViewModel.SelectedIndex - columnsPerRow;

            if (newIndex < 0)
            {
                ViewModel.IsMenuFocused = true;
                _carouselManager?.HideCardSelection();
                FocusMenuIcon(1);

                return;
            }

            ViewModel.SelectedIndex = newIndex;
        }

        private void MoveGridDown()
        {
            if (ViewModel == null) return;

            int columnsPerRow = _carouselManager?.CalculateColumnsPerRow() ?? 3;
            int newIndex = ViewModel.SelectedIndex + columnsPerRow;

            if (newIndex >= ViewModel.Platforms.Count) newIndex = ViewModel.Platforms.Count - 1;

            ViewModel.SelectedIndex = newIndex;
        }

        #endregion
    }
}