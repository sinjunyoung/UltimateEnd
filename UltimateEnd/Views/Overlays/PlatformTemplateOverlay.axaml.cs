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
    public partial class PlatformTemplateOverlay : BaseOverlay
    {
        public event EventHandler<PlatformTemplateInfo>? TemplateSelected;

        public override bool Visible => MainGrid.IsVisible;

        private List<PlatformTemplateInfo> _templates = [];
        private int _selectedIndex = 0;

        public PlatformTemplateOverlay() => InitializeComponent();

        protected override void MovePrevious()
        {
            if (_templates.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _templates.Count) % _templates.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_templates.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _templates.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_templates.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _templates.Count)
            {
                var selected = _templates[_selectedIndex];

                TemplateSelected?.Invoke(this, selected);
            }
        }

        private void UpdateSelection()
        {
            var borders = PlatformTemplateItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.DataContext is PlatformTemplateInfo)
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

        public void SetTemplates(IEnumerable<PlatformTemplateInfo> templates)
        {
            if (_templates != null)
                foreach (var template in _templates)
                    template.Dispose();

            _templates = [.. templates];

            _selectedIndex = _templates.FindIndex(t => t.IsSelected);
            if (_selectedIndex < 0) _selectedIndex = 0;

            PlatformTemplateItemsControl.ItemsSource = _templates;
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

        private void OnTemplateItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is PlatformTemplateInfo template)
            {
                _selectedIndex = _templates.IndexOf(template);
                TemplateSelected?.Invoke(this, template);
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