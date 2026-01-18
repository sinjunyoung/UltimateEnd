using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UltimateEnd.Android.Models;
using UltimateEnd.Enums;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Android.Views.Overlays
{
    public partial class PlatformPickerOverlay : BaseOverlay
    {
        public override bool Visible => MainGrid.IsVisible;
        public event EventHandler<List<string>>? PlatformsConfirmed;

        private readonly ObservableCollection<PlatformItemViewModel> _platforms = [];
        private List<PlatformItemViewModel> _filteredPlatforms = [];
        private List<string> _initialSelected = [];
        private int _selectedIndex = 0;

        public PlatformPickerOverlay()
        {
            InitializeComponent();

            SearchBox.TextChanged += OnSearchTextChanged;            
            LoadPlatforms();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible)
            {
                base.OnKeyDown(e);
                return;
            }

            if (SearchBox.IsFocused && !InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonB, GamepadButton.ButtonA, GamepadButton.Start, GamepadButton.DPadUp, GamepadButton.DPadDown) && e.Key != Key.Space) return;

            if (e.Key == Key.Space ||
                InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;
                ToggleCurrent();
                return;
            }

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

        private void ToggleCurrent()
        {
            if (_filteredPlatforms.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _filteredPlatforms.Count)
            {
                var current = _filteredPlatforms[_selectedIndex];
                current.IsSelected = !current.IsSelected;
                OnClick(EventArgs.Empty);
            }
        }

        private void UpdateSelection()
        {
            var itemBorders = PlatformItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Name == "PlatformItemBorder")
                .ToList();

            if (itemBorders == null || itemBorders.Count == 0) return;

            for (int i = 0; i < itemBorders.Count; i++)
            {
                var border = itemBorders[i];
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
                var database = PlatformInfoService.Instance.GetDatabase();

                foreach (var platform in database.Platforms.OrderBy(p => p.DisplayName))
                {
                    var image = LoadPlatformImage(platform.Id);

                    var vm = new PlatformItemViewModel
                    {
                        Id = platform.Id,
                        DisplayName = platform.DisplayName,
                        Image = image,
                        IsSelected = false
                    };

                    _platforms.Add(vm);
                }

                _filteredPlatforms = [.. _platforms];
                PlatformItemsControl.ItemsSource = _filteredPlatforms;
            }
            catch
            {
            }
        }

        private static Avalonia.Media.Imaging.Bitmap? LoadPlatformImage(string platformId)
        {
            try
            {
                var uri = new Uri(ResourceHelper.GetPlatformImage(platformId));
                if (Avalonia.Platform.AssetLoader.Exists(uri))
                {
                    using var stream = Avalonia.Platform.AssetLoader.Open(uri);
                    return new Avalonia.Media.Imaging.Bitmap(stream);
                }
            }
            catch { }

            return null;
        }

        public void SetSelectedPlatforms(List<string> selectedIds)
        {
            _initialSelected = [.. selectedIds];

            foreach (var platform in _platforms)
                platform.IsSelected = selectedIds.Contains(platform.Id);
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
                _filteredPlatforms = [.. _platforms.Where(p =>
                    p.DisplayName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                    p.Id.Contains(searchText, StringComparison.CurrentCultureIgnoreCase))];

                PlatformItemsControl.ItemsSource = _filteredPlatforms;
            }

            _selectedIndex = 0;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateSelection(),
                Avalonia.Threading.DispatcherPriority.Loaded);
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            _selectedIndex = 0;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateSelection(),
                Avalonia.Threading.DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            Avalonia.Threading.DispatcherTimer.RunOnce(() => MainGrid.IsVisible = false, TimeSpan.FromMilliseconds(300));
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnPlatformItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is PlatformItemViewModel vm)
            {
                vm.IsSelected = !vm.IsSelected;
                OnClick(EventArgs.Empty);
            }

            e.Handled = true;
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Hide(HiddenState.Cancel);
            e.Handled = true;
        }

        private void OnConfirmClick(object? sender, RoutedEventArgs e)
        {
            var selected = _platforms.Where(p => p.IsSelected).Select(p => p.Id).ToList();
            PlatformsConfirmed?.Invoke(this, selected);
            Hide(HiddenState.Close);
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