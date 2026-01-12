using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;

namespace UltimateEnd.Views.Overlays
{
    public partial class SettingsMenuOverlay : BaseOverlay
    {
        public event EventHandler? EmulatorClicked;
        public event EventHandler? ScrapClicked;
        public event EventHandler? PlaylistClicked;
        public event EventHandler? ManageIgnoreGameClicked;
        public event EventHandler? ResetLayoutClicked;
        //public event EventHandler? PlatformTemplateClicked;
        public event EventHandler? PlatformImageClicked;
        public event EventHandler? PegasusMetadataClicked;
        public event EventHandler? EsDeMetadataClicked;

        public override bool Visible => MainGrid.IsVisible;

        private int _selectedIndex = 0;
        private readonly List<Border> _menuItems = [];
        private Dictionary<string, Action> _menuActions = [];

        private bool _isShowingDeleted = false;

        private GameViewMode _currentViewMode;

        public SettingsMenuOverlay()
        {
            InitializeComponent();
            InitializeMenuActions();            
        }

        private void InitializeMenuActions()
        {
            _menuActions = new Dictionary<string, Action>
            {
                ["EmulatorMenuItem"] = () => EmulatorClicked?.Invoke(this, EventArgs.Empty),
                ["ScrapMenuItem"] = () => ScrapClicked?.Invoke(this, EventArgs.Empty),
                ["PlaylistMenuItem"] = () => PlaylistClicked?.Invoke(this, EventArgs.Empty),
                ["ManageIgnoreGameMenuItem"] = () => ManageIgnoreGameClicked?.Invoke(this, EventArgs.Empty),
                ["ResetLayoutMenuItem"] = () => ResetLayoutClicked?.Invoke(this, EventArgs.Empty),
                //["PlatformTemplateMenuItem"] = () => PlatformTemplateClicked?.Invoke(this, EventArgs.Empty),
                ["PlatformImageMenuItem"] = () => PlatformImageClicked?.Invoke(this, EventArgs.Empty),
                ["PegasusMetadataMenuItem"] = () => PegasusMetadataClicked?.Invoke(this, EventArgs.Empty),
                ["EsDeMetadataMenuItem"] = () => EsDeMetadataClicked?.Invoke(this, EventArgs.Empty)
            };
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

        protected override void SelectCurrent()
        {
            if (_menuItems.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _menuItems.Count)
            {
                var selected = _menuItems[_selectedIndex];
                if (selected.Name != null && _menuActions.TryGetValue(selected.Name, out var action))
                    action?.Invoke();
            }
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

            var items = SettingsMenuPanel?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Name != null && b.Name.EndsWith("MenuItem"))
                .ToList();

            if (items != null)
                _menuItems.AddRange(items);
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            UpdateDeletedGamesToggle(this._isShowingDeleted);
            UpdateResetLayoutVisibility();

            Dispatcher.UIThread.Post(() =>
            {
                InitializeMenuItems();
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

        //private void OnPlatformTemplateClick(object? sender, RoutedEventArgs e)
        //{
        //    UpdateSelectedIndexFromSender(sender);
        //    PlatformTemplateClicked?.Invoke(this, EventArgs.Empty);
        //    e.Handled = true;
        //}

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
            if (e.Source == sender)
                Hide(HiddenState.Close);
        }

        private static void UpdateToggle(Border toggleBack, Border toggle, bool value)
        {
            string resourceKey = value ? "Toggle.SelectionBackground" : "Toggle.Background";

            if (Avalonia.Application.Current != null &&
                Avalonia.Application.Current.Resources.TryGetResource(resourceKey,
                    Avalonia.Application.Current?.ActualThemeVariant, out object? resourceObj))
                toggleBack.Background = resourceObj as IBrush;

            toggle.HorizontalAlignment = value
                ? Avalonia.Layout.HorizontalAlignment.Right
                : Avalonia.Layout.HorizontalAlignment.Left;
        }

        public void UpdateDeletedGamesToggle(bool isShowingDeleted)
        {
            var toggleBack = this.FindControl<Border>("DeletedGamesToggle");
            var toggle = this.FindControl<Border>("DeletedGamesToggleThumb");

            if (toggleBack != null && toggle != null)
            {
                UpdateToggle(toggleBack, toggle, isShowingDeleted);
            }
        }

        public void SetDeletedGamesMode(bool isShowingDeleted)
        {
            _isShowingDeleted = isShowingDeleted;
            UpdateDeletedGamesToggle(isShowingDeleted);
        }

        public void UpdateViewMode(GameViewMode viewMode)
        {
            _currentViewMode = viewMode;
            UpdateResetLayoutVisibility();
        }

        private void UpdateResetLayoutVisibility()
        {
            var resetLayoutItem = this.FindControl<Border>("ResetLayoutMenuItem");

            if (resetLayoutItem != null)
                resetLayoutItem.IsVisible = _currentViewMode == GameViewMode.List;

            RefreshMenuItems();
        }

        private void RefreshMenuItems()
        {
            _menuItems.Clear();
            InitializeMenuItems();

            if (_selectedIndex >= _menuItems.Count)
                _selectedIndex = Math.Max(0, _menuItems.Count - 1);

            UpdateSelection();
        }
    }
}