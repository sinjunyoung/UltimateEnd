using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Views.Overlays
{
    public partial class GameEmulatorSelectionOverlay : BaseOverlay
    {
        public event EventHandler? DefaultSelected;
        public event EventHandler<EmulatorInfo>? EmulatorSelected;

        public override bool Visible => MainGrid.IsVisible;

        private readonly ObservableCollection<EmulatorInfo> _emulators = [];
        private int _selectedIndex = -1;
        private Border? _defaultButton;

        public GameEmulatorSelectionOverlay()
        {
            InitializeComponent();
            GameEmulatorItemsControl.ItemsSource = _emulators;

        }

        protected override void MovePrevious()
        {
            if (_emulators.Count == 0) return;

            if (_selectedIndex == 0)
                _selectedIndex = -1;
            else if (_selectedIndex == -1)
                _selectedIndex = _emulators.Count - 1;
            else
                _selectedIndex--;

            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_emulators.Count == 0) return;

            if (_selectedIndex == _emulators.Count - 1)
                _selectedIndex = -1;
            else if (_selectedIndex == -1)
                _selectedIndex = 0;
            else
                _selectedIndex = (_selectedIndex + 1) % _emulators.Count;

            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_selectedIndex == -1)
                DefaultSelected?.Invoke(this, EventArgs.Empty);
            else if (_selectedIndex >= 0 && _selectedIndex < _emulators.Count)
            {
                var selected = _emulators[_selectedIndex];
                EmulatorSelected?.Invoke(this, selected);
            }
        }

        private void UpdateSelection()
        {
            if (_defaultButton == null)
            {
                _defaultButton = GameEmulatorPanel?.GetVisualDescendants()
                    .OfType<Border>()
                    .FirstOrDefault(b => b.GetVisualDescendants().Any(c => c is TextBlock tb && tb.Text == "플랫폼 기본값 사용"));
            }

            if (_defaultButton != null)
            {
                if (_selectedIndex == -1)
                {
                    _defaultButton.Background = this.FindResource("Background.Hover") as IBrush;
                    _defaultButton.BringIntoView();
                }
                else
                    _defaultButton.Background = this.FindResource("Background.Secondary") as IBrush;

                var borders = GameEmulatorItemsControl?.GetVisualDescendants()
                    .OfType<Border>()
                    .Where(b => b.DataContext is EmulatorInfo)
                    .ToList();

                if (borders == null) return;

                for (int i = 0; i < borders.Count; i++)
                {
                    var border = borders[i];
                    if (i == _selectedIndex)
                    {
                        border.Background = this.FindResource("Background.Hover") as IBrush;
                        border.BringIntoView();
                    }
                    else
                        border.Background = this.FindResource("Background.Secondary") as IBrush;
                }
            }
        }

        public void SetEmulators(GameMetadata game, IEnumerable<EmulatorInfo> emulators)
        {
            foreach (var emulator in _emulators)
                if (emulator is IDisposable disposable)
                    disposable.Dispose();

            _emulators.Clear();

            foreach (var emulator in emulators)
                _emulators.Add(emulator);

            DefaultEmulatorCheck.IsVisible = string.IsNullOrEmpty(game.EmulatorId);

            if (string.IsNullOrEmpty(game.EmulatorId))
                _selectedIndex = -1;
            else
            {
                _selectedIndex = _emulators
                    .Select((emulator, index) => new { emulator, index })
                    .FirstOrDefault(x => x.emulator.Id == game.EmulatorId) ?.index ?? -1;

                if (_selectedIndex < 0) _selectedIndex = -1;
            }

            foreach (var emulator in _emulators)
                emulator.IsSelected = emulator.Id == game.EmulatorId;
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            _defaultButton = null;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateSelection(),
                Avalonia.Threading.DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            MainGrid.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnGameEmulatorDefaultTapped(object? sender, TappedEventArgs e)
        {
            _selectedIndex = -1;
            DefaultEmulatorCheck.IsVisible = true;
            DefaultSelected?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnGameEmulatorItemTapped(object? sender, TappedEventArgs e)
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
                Hide(HiddenState.Close);
        }
    }
}