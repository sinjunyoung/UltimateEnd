using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Views.Overlays
{
    public partial class PlatformThemeOverlay : BaseOverlay
    {
        public event EventHandler<ThemeOption>? ThemeSelected;
        public override bool Visible => this.IsVisible;

        private readonly List<ThemeOption> _themes = [];
        private int _selectedIndex = 0;

        public PlatformThemeOverlay()
        {
            InitializeComponent();
            this.IsVisible = false;
        }

        protected override void MovePrevious()
        {
            if (_themes.Count == 0) return;

            _selectedIndex = (_selectedIndex - 1 + _themes.Count) % _themes.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_themes.Count == 0) return;

            _selectedIndex = (_selectedIndex + 1) % _themes.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_themes.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _themes.Count)
            {
                var selected = _themes[_selectedIndex];
                ThemeSelected?.Invoke(this, selected);
                Hide(HiddenState.Confirm);
            }
        }

        private void UpdateSelection()
        {
            var itemsControl = MainBorder?.GetVisualDescendants()
                .OfType<ItemsControl>()
                .FirstOrDefault();

            if (itemsControl == null) return;

            var borders = new List<Border>();

            foreach (var presenter in itemsControl.GetVisualDescendants().OfType<ContentPresenter>())
            {
                var rootBorder = presenter.GetVisualChildren()
                    .OfType<Border>()
                    .FirstOrDefault(b => b.DataContext is ThemeOption);

                if (rootBorder != null)
                    borders.Add(rootBorder);
            }

            if (borders.Count == 0) return;

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

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            this.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var itemsControl = MainBorder?.GetVisualDescendants()
                    .OfType<ItemsControl>()
                    .FirstOrDefault();

                if (itemsControl?.ItemsSource is System.Collections.IEnumerable enumerable)
                {
                    _themes.Clear();
                    foreach (var item in enumerable)
                    {
                        if (item is ThemeOption theme)
                            _themes.Add(theme);
                    }

                    _selectedIndex = _themes.FindIndex(t => t.IsSelected);
                    if (_selectedIndex < 0) _selectedIndex = 0;

                    UpdateSelection();
                }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            this.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnBackClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            Hide(HiddenState.Cancel);
        }

        private void OnThemeItemTapped(object? sender, TappedEventArgs e)
        {
            e.Handled = true;
            if (sender is Border border && border.DataContext is ThemeOption theme)
            {
                _selectedIndex = _themes.IndexOf(theme);
                ThemeSelected?.Invoke(this, theme);
                Hide(HiddenState.Confirm);
            }
        }
    }
}