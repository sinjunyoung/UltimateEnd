using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Views.Overlays
{
    public partial class EmulatorSelectionOverlay : BaseOverlay
    {
        public event EventHandler<EmulatorInfo>? EmulatorSelected;
        public override bool Visible => MainGrid.IsVisible;

        private List<EmulatorInfo> _emulators = [];
        private int _selectedIndex = 0;

        public EmulatorSelectionOverlay() => InitializeComponent();

        protected override void MovePrevious()
        {
            if (_emulators.Count == 0) return;

            _selectedIndex = (_selectedIndex - 1 + _emulators.Count) % _emulators.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_emulators.Count == 0) return;

            _selectedIndex = (_selectedIndex + 1) % _emulators.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_emulators.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _emulators.Count)
            {
                var selected = _emulators[_selectedIndex];
                EmulatorSelected?.Invoke(this, selected);
            }
        }

        private void UpdateSelection()
        {
            var borders = EmulatorItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.DataContext is EmulatorInfo)
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

        public void SetEmulators(IEnumerable<EmulatorInfo> emulators)
        {
            _emulators = [.. emulators];
            _selectedIndex = 0;

            var defaultIndex = _emulators.FindIndex(e => e.IsDefault);
            if (defaultIndex >= 0)
                _selectedIndex = defaultIndex;

            EmulatorItemsControl.ItemsSource = _emulators;
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
            MainGrid.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnEmulatorItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is EmulatorInfo emulator)
            {
                _selectedIndex = _emulators.IndexOf(emulator);
                EmulatorSelected?.Invoke(this, emulator);
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