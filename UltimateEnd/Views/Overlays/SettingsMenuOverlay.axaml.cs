using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class SettingsMenuOverlay : BaseOverlay
    {
        public event EventHandler? EmulatorClicked;
        public event EventHandler? ScrapClicked;
        public event EventHandler? PlaylistClicked;
        public event EventHandler? ManageIgnoreGameClicked;
        public event EventHandler? GridColumnsChanged;
        public event EventHandler? SimpleGameListClicked;
        public event EventHandler? ResetLayoutClicked;
        public event EventHandler? PlatformImageClicked;
        public event EventHandler? PegasusMetadataClicked;
        public event EventHandler? EsDeMetadataClicked;

        public override bool Visible => MainGrid.IsVisible;

        private int _selectedIndex = 0;
        private readonly List<Border> _menuItems = [];
        private readonly Dictionary<int, Action> _menuActions = [];

        private bool _isShowingDeleted = false;
        private GameViewMode _currentViewMode;
        private bool _simpleGameListMode = false;

        public SettingsMenuOverlay() => InitializeComponent();

        public void UpdateViewMode(GameViewMode viewMode)
        {
            _currentViewMode = viewMode;
            UpdateResetLayoutVisibility();
            UpdateGridColumnsVisibility();
            UpdateSimpleGameListVisibility();
        }

        private void UpdateMenuActions()
        {
            _menuActions.Clear();

            for (int i = 0; i < _menuItems.Count; i++)
            {
                var item = _menuItems[i];
                int index = i;

                _menuActions[index] = item.Name switch
                {
                    "EmulatorMenuItem" => () => EmulatorClicked?.Invoke(this, EventArgs.Empty),
                    "ScrapMenuItem" => () => ScrapClicked?.Invoke(this, EventArgs.Empty),
                    "PlaylistMenuItem" => () => PlaylistClicked?.Invoke(this, EventArgs.Empty),
                    "ManageIgnoreGameMenuItem" => () => ManageIgnoreGameClicked?.Invoke(this, EventArgs.Empty),
                    "GridColumnsMenuItem" => () => { },
                    "SimpleGameListMenuItem" => () => SimpleGameListClicked?.Invoke(this, EventArgs.Empty),
                    "ResetLayoutMenuItem" => () => ResetLayoutClicked?.Invoke(this, EventArgs.Empty),
                    "PlatformImageMenuItem" => () => PlatformImageClicked?.Invoke(this, EventArgs.Empty),
                    "PegasusMetadataMenuItem" => () => PegasusMetadataClicked?.Invoke(this, EventArgs.Empty),
                    "EsDeMetadataMenuItem" => () => EsDeMetadataClicked?.Invoke(this, EventArgs.Empty),
                    _ => () => { }
                };
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

        protected async override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible)
            {
                base.OnKeyDown(e);
                return;
            }

            if (_selectedIndex >= 0 && _selectedIndex < _menuItems.Count &&
                _menuItems[_selectedIndex].Name == "GridColumnsMenuItem")
            {
                if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
                {
                    await WavSounds.Click();
                    if (GridColumnsSlider != null)
                        GridColumnsSlider.Value = Math.Max(GridColumnsSlider.Minimum, GridColumnsSlider.Value - 1);
                    e.Handled = true;
                    return;
                }
                if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
                {
                    await WavSounds.Click();
                    if (GridColumnsSlider != null)
                        GridColumnsSlider.Value = Math.Min(GridColumnsSlider.Maximum, GridColumnsSlider.Value + 1);
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        protected override void MovePrevious()
        {
            if (_menuItems.Count == 0) return;

            int attempts = 0;
            do
            {
                _selectedIndex = (_selectedIndex - 1 + _menuItems.Count) % _menuItems.Count;
                attempts++;
            }
            while (_menuItems[_selectedIndex].IsVisible == false && attempts < _menuItems.Count);

            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_menuItems.Count == 0) return;

            int attempts = 0;
            do
            {
                _selectedIndex = (_selectedIndex + 1) % _menuItems.Count;
                attempts++;
            }
            while (_menuItems[_selectedIndex].IsVisible == false && attempts < _menuItems.Count);

            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_menuActions.TryGetValue(_selectedIndex, out var action)) action?.Invoke();
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
                    item.Background = this.FindResource("Background.Secondary") as IBrush;
            }
        }

        private void InitializeMenuItems()
        {
            if (_menuItems.Count > 0) return;

            var stackPanel = SettingsMenuPanel?.GetVisualDescendants()
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

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            UpdateDeletedGamesToggle(_isShowingDeleted);
            UpdateResetLayoutVisibility();

            Dispatcher.UIThread.Post(() =>
            {
                InitializeMenuItems();
                UpdateMenuActions();
                UpdateSelection();
            }, DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            DispatcherTimer.RunOnce(() => MainGrid.IsVisible = false, TimeSpan.FromMilliseconds(300));
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnEmulatorClick(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            EmulatorClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnScrapClick(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            ScrapClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnPlaylistClick(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            PlaylistClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnSimpleGameListClick(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            SimpleGameListClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnResetLayoutClick(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            ResetLayoutClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnManageIgnoreGameClick(object sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            ManageIgnoreGameClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnPlatformImageClick(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            PlatformImageClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnPegasusMetadataClick(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            PegasusMetadataClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnEsDeMetadataClick(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            EsDeMetadataClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnClose(object? sender, PointerPressedEventArgs e)
        {
            Hide(HiddenState.Close);
            e.Handled = true;
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) Hide(HiddenState.Close);
        }

        private static void UpdateToggle(Border toggleBack, Border toggle, bool value)
        {
            string resourceKey = value ? "Toggle.SelectionBackground" : "Toggle.Background";

            if (Avalonia.Application.Current != null && Avalonia.Application.Current.Resources.TryGetResource(resourceKey, Avalonia.Application.Current?.ActualThemeVariant, out object? resourceObj)) toggleBack.Background = resourceObj as IBrush;

            toggle.HorizontalAlignment = value ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        }

        public void UpdateDeletedGamesToggle(bool isShowingDeleted)
        {
            if (DeletedGamesToggle != null && DeletedGamesToggleThumb != null) UpdateToggle(DeletedGamesToggle, DeletedGamesToggleThumb, isShowingDeleted);
        }

        public void SetDeletedGamesMode(bool isShowingDeleted)
        {
            _isShowingDeleted = isShowingDeleted;
            UpdateDeletedGamesToggle(isShowingDeleted);
        }

        public void UpdateSimpleGameListToggle(bool isSimpleMode)
        {
            if (SimpleGameListToggle != null && SimpleGameListToggleThumb != null) UpdateToggle(SimpleGameListToggle, SimpleGameListToggleThumb, isSimpleMode);
        }

        public void SetSimpleGameListMode(bool isSimpleMode)
        {
            _simpleGameListMode = isSimpleMode;
            UpdateSimpleGameListToggle(isSimpleMode);
        }

        private void UpdateResetLayoutVisibility()
        {
            ResetLayoutMenuItem.IsVisible = _currentViewMode == GameViewMode.List;
            RefreshMenuItems();
        }

        private void UpdateGridColumnsVisibility()
        {
            GridColumnsMenuItem.IsVisible = _currentViewMode == GameViewMode.Grid;

            if (_currentViewMode == GameViewMode.Grid && GridColumnsSlider != null)
            {
                var settings = Services.SettingsService.LoadSettings();

                GridColumnsSlider.Minimum = 2;
                GridColumnsSlider.Maximum = OperatingSystem.IsAndroid() ? 7 : 10;
                GridColumnsSlider.Value = settings.GridColumns;
                UpdateGridColumnsText(settings.GridColumns);
            }

            RefreshMenuItems();
        }

        private void UpdateSimpleGameListVisibility()
        {
            SimpleGameListMenuItem.IsVisible = _currentViewMode == GameViewMode.List;
            RefreshMenuItems();
        }

        private void OnGridColumnsSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (sender is Slider slider)
            {
                int value = (int)slider.Value;
                UpdateGridColumnsText(value);

                var settings = Services.SettingsService.LoadSettings();
                settings.GridColumns = value;
                Services.SettingsService.SaveGameListSettings(settings);

                GridColumnsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UpdateGridColumnsText(int value)
        {
            if (GridColumnsValue != null) GridColumnsValue.Text = value == 2 ? "ÀÚµ¿" : $"{value}¿­";
        }

        private void RefreshMenuItems()
        {
            _menuItems.Clear();
            InitializeMenuItems();
            UpdateMenuActions();

            if (_selectedIndex >= _menuItems.Count)
                _selectedIndex = Math.Max(0, _menuItems.Count - 1);

            UpdateSelection();
        }
    }
}