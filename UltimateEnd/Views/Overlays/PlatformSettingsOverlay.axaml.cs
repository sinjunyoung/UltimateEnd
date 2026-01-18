using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class PlatformSettingsOverlay : BaseOverlay
    {
        public override bool Visible => this.IsVisible;

        private readonly List<Border> _menuItems = [];
        private int _selectedIndex = 0;
        private int _pendingScreensaverTimeout = -1;

        public event EventHandler? ThemeClicked;
        public event EventHandler? BackgroundImageClicked;
        public event EventHandler? EmulatorClicked;
        public event EventHandler? PlaylistClicked;
        public event EventHandler<int>? ScreensaverTimeoutChanged;
        public event EventHandler? ScrapClicked;
        public event EventHandler? KeyBindingClicked;
        public event EventHandler? GenreClicked;

        private Dictionary<int, Action> _menuActions = [];

        public PlatformSettingsOverlay()
        {
            InitializeComponent();

            var ver = PlatformServiceFactory.Create?.Invoke();
            VersionText.Text = $"{ver.GetAppName()} Ver {ver.GetAppVersion()}";

            this.IsVisible = false;
            InitializeMenuActions();
        }

        private void InitializeMenuActions()
        {
            _menuActions = new Dictionary<int, Action>
            {
                [0] = () => OnThemeClickAction(),
                [1] = () => OnBackgroundImageClickAction(),                
                [2] = () => OnEmulatorClickAction(),
                [3] = () => OnPlaylistClickAction(),
                [4] = () => OnSaveBackupModeClickAction(),
                [5] = () => OnToggleNativeAppPlatformAction(),
                [6] = () => OnScreensaverTimeoutClickAction(),
                [7] = () => OnScrapClickAction(),
                [8] = () => OnKeyBindingClickAction(),
                [9] = () => OnGenreClickAction()
            };
        }

        protected override void SelectCurrent()
        {
            if (_menuActions.TryGetValue(_selectedIndex, out var action))
                action?.Invoke();
        }

        protected override void MovePrevious()
        {
            if (_menuItems.Count == 0) return;

            _selectedIndex = (_selectedIndex - 1 + _menuItems.Count) % _menuItems.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_menuItems.Count == 0) return;

            _selectedIndex = (_selectedIndex + 1) % _menuItems.Count;
            UpdateSelection();
        }

        protected async override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible)
            {
                base.OnKeyDown(e);
                return;
            }

            if (_selectedIndex == 6)
            {
                if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
                {
                    await WavSounds.Click();
                    var slider = this.FindControl<Slider>("ScreensaverTimeoutSlider");
                    if (slider != null)
                    {
                        slider.Value = Math.Max(slider.Minimum, slider.Value - 1);
                    }
                    e.Handled = true;
                    return;
                }
                if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
                {
                    await WavSounds.Click();
                    var slider = this.FindControl<Slider>("ScreensaverTimeoutSlider");
                    if (slider != null)
                    {
                        slider.Value = Math.Min(slider.Maximum, slider.Value + 1);
                    }
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < _menuItems.Count; i++)
            {
                var item = _menuItems[i];

                if (i == _selectedIndex)
                {
                    item.Background = this.FindResource("Background.Hover") as IBrush;
                    item.BringIntoView();
                }
                else
                {
                    item.Background = this.FindResource("Background.Secondary") as IBrush;
                }
            }
        }

        private void InitializeMenuItems()
        {
            if (_menuItems.Count > 0) return;

            var stackPanel = SettingsPanel?.GetVisualDescendants()
                .OfType<StackPanel>()
                .FirstOrDefault();

            if (stackPanel != null)
            {
                var items = stackPanel.Children
                    .OfType<Border>()
                    .Where(b => b.CornerRadius.TopLeft == 8)
                    .ToList();

                _menuItems.AddRange(items);
            }
        }

        private void UpdateSelectedIndexFromSender(object? sender)
        {
            if (sender is Border border && _menuItems.Count > 0)
            {
                var index = _menuItems.IndexOf(border);
                if (index >= 0)
                {
                    _selectedIndex = index;
                    UpdateSelection();
                }
            }
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            var settings = Services.SettingsService.LoadSettings();
            var slider = this.FindControl<Slider>("ScreensaverTimeoutSlider");

            if (slider != null)
                slider.Value = settings.ScreensaverTimeoutMinutes;

            var saveBackupModeText = this.FindControl<TextBlock>("SaveBackupModeText");
            if (saveBackupModeText != null)
            {
                saveBackupModeText.Text = settings.SaveBackupMode switch
                {
                    SaveBackupMode.SaveState => "세이브 스테이트",
                    SaveBackupMode.Both => "모두 백업",
                    _ => "일반 세이브"
                };
            }

            UpdateToggle(NativeAppPlatformToggle, NativeAppPlatformToggleThumb, settings.ShowNativeAppPlatform);
            
            this.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            Dispatcher.UIThread.Post(() =>
            {
                InitializeMenuItems();
                _selectedIndex = 0;
                UpdateSelection();
            }, DispatcherPriority.Loaded);
        }

        private void UpdateToggle(Border toggleBack, Border toggle, bool value)
        {
            string resourceKey = value ? "Toggle.SelectionBackground" : "Toggle.Background";

            if (this.TryFindResource(resourceKey, out object? resource) && resource is IBrush brush)
                toggleBack.Background = brush;

            toggle.HorizontalAlignment = value
                ? Avalonia.Layout.HorizontalAlignment.Right
                : Avalonia.Layout.HorizontalAlignment.Left;
        }

        public override void Hide(HiddenState state)
        {
            if (_pendingScreensaverTimeout >= 0)
            {
                var settings = Services.SettingsService.LoadSettings();
                settings.ScreensaverTimeoutMinutes = _pendingScreensaverTimeout;
                Services.SettingsService.SaveSettingsQuiet(settings);

                ScreensaverTimeoutChanged?.Invoke(this, _pendingScreensaverTimeout);

                _pendingScreensaverTimeout = -1;
            }

            this.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnCloseClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            Hide(HiddenState.Cancel);
        }

        private void OnOverlayBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                OnCloseClick(sender, e);
        }

        private async void OnThemeClickAction()
        {
            await WavSounds.OK();
            ThemeClicked?.Invoke(this, EventArgs.Empty);        
        }

        private async void OnBackgroundImageClickAction()
        {
            await WavSounds.OK();
            BackgroundImageClicked?.Invoke(this, EventArgs.Empty);
        }

        private async void OnEmulatorClickAction()
        {
            await WavSounds.OK();
            EmulatorClicked?.Invoke(this, EventArgs.Empty);
        }

        private async void OnPlaylistClickAction()
        {
            await WavSounds.OK();
            PlaylistClicked?.Invoke(this, EventArgs.Empty);
        }

        private async void OnSaveBackupModeClickAction()
        {
            await WavSounds.OK();

            var settings = SettingsService.LoadSettings();
            var textBlock = this.FindControl<TextBlock>("SaveBackupModeText");

            settings.SaveBackupMode = settings.SaveBackupMode switch
            {
                SaveBackupMode.NormalSave => SaveBackupMode.SaveState,
                SaveBackupMode.SaveState => SaveBackupMode.Both,
                SaveBackupMode.Both => SaveBackupMode.NormalSave,
                _ => SaveBackupMode.NormalSave
            };

            if (textBlock != null)
            {
                textBlock.Text = settings.SaveBackupMode switch
                {
                    SaveBackupMode.SaveState => "세이브 스테이트",
                    SaveBackupMode.Both => "모두 백업",
                    _ => "일반 세이브"
                };
            }

            SettingsService.SaveSettingsQuiet(settings);
        }

        private async void OnToggleNativeAppPlatformAction()
        {
            await WavSounds.OK();

            var settings = SettingsService.LoadSettings();
            settings.ShowNativeAppPlatform = !settings.ShowNativeAppPlatform;
            SettingsService.SaveSettingsQuiet(settings);

            UpdateToggle(NativeAppPlatformToggle, NativeAppPlatformToggleThumb, settings.ShowNativeAppPlatform);

            SettingsService.SavePlatformSettings(settings);
        }

        private void OnScreensaverTimeoutClickAction() { }

        private void OnScreensaverTimeoutChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (sender is Slider slider)
            {
                int minutes = (int)slider.Value;

                var valueText = this.FindControl<TextBlock>("ScreensaverTimeoutValueText");

                if (valueText != null)
                {
                    if (minutes == 0)
                        valueText.Text = "사용 안 함";
                    else
                        valueText.Text = $"{minutes}분";
                }

                _pendingScreensaverTimeout = minutes;
            }
        }

        private async void OnScrapClickAction()
        {
            await WavSounds.OK();
            ScrapClicked?.Invoke(this, EventArgs.Empty);
        }

        private async void OnKeyBindingClickAction()
        {
            await WavSounds.OK();
            KeyBindingClicked?.Invoke(this, EventArgs.Empty);
        }

        private async void OnGenreClickAction()
        {
            await WavSounds.OK();
            GenreClicked?.Invoke(this, EventArgs.Empty);
        }

        private void OnThemeClick(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnThemeClickAction();
        }

        private void OnBackgroundImageClick(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnBackgroundImageClickAction();
        }

        private void OnEmulatorClick(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnEmulatorClickAction();
        }

        private void OnPlaylistClick(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnPlaylistClickAction();
        }

        private void OnSaveBackupModeClick(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnSaveBackupModeClickAction();
        }

        private void OnToggleNativeAppPlatform(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnToggleNativeAppPlatformAction();
        }

        private void OnScrapClick(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnScrapClickAction();
        }

        private void OnKeyBindingClick(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnKeyBindingClickAction();
        }

        private void OnGenreClick(object? sender, TappedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;
            OnGenreClickAction();
        }
    }
}