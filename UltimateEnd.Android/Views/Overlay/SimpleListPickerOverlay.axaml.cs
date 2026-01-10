using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Android.Views.Overlays
{
    public partial class SimpleListPickerOverlay : BaseOverlay
    {
        public override bool Visible => MainGrid.IsVisible;
        public event EventHandler<string>? ItemSelected;

        private List<string> _items = [];
        private int _selectedIndex = 0;

        public SimpleListPickerOverlay() => InitializeComponent();

        protected override void MovePrevious()
        {
            if (_items.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _items.Count) % _items.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_items.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _items.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_items.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _items.Count)
            {
                var selected = _items[_selectedIndex];
                ItemSelected?.Invoke(this, selected);
                Hide(HiddenState.Confirm);
            }
        }

        private void UpdateSelection()
        {
            var borders = ItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.DataContext is string)
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

        public void SetTitle(string title) => TitleText.Text = title;

        public void SetItems(List<string> items, string? selectedItem = null)
        {
            _items = items;
            ItemsControl.ItemsSource = _items;

            if (!string.IsNullOrEmpty(selectedItem))
            {
                var index = _items.IndexOf(selectedItem);
                _selectedIndex = index >= 0 ? index : 0;
            }
            else
                _selectedIndex = 0;
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateSelection(),
                Avalonia.Threading.DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            Avalonia.Threading.DispatcherTimer.RunOnce(() => MainGrid.IsVisible = false, TimeSpan.FromMilliseconds(300));
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is string item)
            {
                OnClick(EventArgs.Empty);
                ItemSelected?.Invoke(this, item);
                Hide(HiddenState.Confirm);
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