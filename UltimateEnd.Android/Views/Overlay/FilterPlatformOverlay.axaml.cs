using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Android.Views.Overlays
{
    public partial class FilterPlatformOverlay : BaseOverlay
    {
        public override bool Visible => MainGrid.IsVisible;
        public event EventHandler<PlatformInfo>? PlatformSelected;

        private readonly List<PlatformInfo> _platforms = [];
        private List<PlatformInfo> _filteredPlatforms = [];
        private int _selectedIndex = 0;

        public FilterPlatformOverlay()
        {
            InitializeComponent();

            SearchBox.TextChanged += OnSearchTextChanged;
            LoadPlatforms();
        }

        public void SetSelectedPlatform(PlatformInfo? selectedPlatform)
        {
            if (selectedPlatform != null && !string.IsNullOrEmpty(selectedPlatform.Id))
            {
                var index = _filteredPlatforms.FindIndex(p =>
                    p.Id != null && p.Id.Equals(selectedPlatform.Id, StringComparison.OrdinalIgnoreCase));

                if (index >= 0)
                    _selectedIndex = index;
                else
                    _selectedIndex = 0;
            }
            else
                _selectedIndex = 0;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible) return;

            if (SearchBox.IsFocused &&
                !InputManager.IsAnyButtonPressed(e.Key,
                    GamepadButton.ButtonB,
                    GamepadButton.ButtonA,
                    GamepadButton.Start,
                    GamepadButton.DPadUp,
                    GamepadButton.DPadDown))
                return;

            base.OnKeyDown(e);
        }

        protected override void MovePrevious()
        {
            if (_filteredPlatforms.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _filteredPlatforms.Count) % _filteredPlatforms.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_filteredPlatforms.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _filteredPlatforms.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_filteredPlatforms.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _filteredPlatforms.Count)
            {
                var selected = _filteredPlatforms[_selectedIndex];
                PlatformSelected?.Invoke(this, selected);
                Hide(HiddenState.Close);
            }
        }

        private void UpdateSelection()
        {
            var borders = PlatformItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.DataContext is PlatformInfo)
                .ToList();

            if (borders == null || borders.Count == 0) return;

            for (int i = 0; i < borders.Count; i++)
            {
                var border = borders[i];
                if (i == _selectedIndex)
                {
                    border.Background = this.FindResource("Background.Hover") as IBrush;
                    border.BringIntoView();
                }
                else
                    border.Background = Brushes.Transparent;
            }
        }

        private void LoadPlatforms()
        {
            try
            {
                _platforms.Add(new PlatformInfo
                {
                    Id = null!,
                    DisplayName = "ÀüÃ¼",
                    Image = null!
                });

                var database = PlatformInfoService.LoadDatabase();

                foreach (var platform in database.Platforms.OrderBy(p => p.DisplayName))
                {
                    var image = LoadPlatformImage(platform.Id);

                    _platforms.Add(new PlatformInfo
                    {
                        Id = platform.Id,
                        DisplayName = platform.DisplayName,
                        Image = image
                    });
                }

                _filteredPlatforms = [.. _platforms];
                PlatformItemsControl.ItemsSource = _filteredPlatforms;
            }
            catch { }
        }

        private static Bitmap LoadPlatformImage(string platformId)
        {
            try
            {
                var uri = new Uri(ResourceHelper.GetPlatformImage(platformId));
                if (Avalonia.Platform.AssetLoader.Exists(uri))
                {
                    using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                    return new Bitmap(stream);
                }
            }
            catch { }

            return null;
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.ToLower() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredPlatforms = [.. _platforms];
                PlatformItemsControl.ItemsSource = _filteredPlatforms;
            }
            else
            {
                _filteredPlatforms = _platforms.Where(p =>
                    p.DisplayName.ToLower().Contains(searchText) ||
                    (p.Id != null && p.Id.ToLower().Contains(searchText))).ToList();

                PlatformItemsControl.ItemsSource = _filteredPlatforms;
            }

            _selectedIndex = 0;

            Dispatcher.UIThread.Post(() => UpdateSelection(), DispatcherPriority.Loaded);
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            Dispatcher.UIThread.Post(() => UpdateSelection(), DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            DispatcherTimer.RunOnce(() => MainGrid.IsVisible = false, TimeSpan.FromMilliseconds(300));
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnPlatformItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is PlatformInfo platform)
            {
                OnClick(EventArgs.Empty);
                PlatformSelected?.Invoke(this, platform);
                Hide(HiddenState.Close);
            }
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
                Hide(HiddenState.Cancel);
        }
    }
}