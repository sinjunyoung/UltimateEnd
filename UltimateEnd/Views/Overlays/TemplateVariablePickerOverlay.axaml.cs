using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Views.Overlays
{
    public partial class TemplateVariablePickerOverlay : BaseOverlay
    {
        public override bool Visible => MainGrid.IsVisible;
        public event EventHandler<string>? VariableSelected;
        
        private readonly List<TemplateVariable> _variables = TemplateVariableManagerFactory.Create?.Invoke().Variables;

        private int _selectedIndex = 0;

        public TemplateVariablePickerOverlay()
        {
            InitializeComponent();
            VariableItemsControl.ItemsSource = _variables;
        }

        protected override void MovePrevious()
        { 
            if (_variables.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _variables.Count) % _variables.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_variables.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _variables.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_variables.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _variables.Count)
            {
                var selected = _variables[_selectedIndex];
                VariableSelected?.Invoke(this, selected.Variable);
                Hide(HiddenState.Close);
                OnClick(EventArgs.Empty);
            }
        }

        private void UpdateSelection()
        {
            var borders = VariableItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.DataContext is TemplateVariable)
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
                    border.Background = this.FindResource("Background.Secondary") as IBrush;
            }
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
            MainGrid.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnVariableItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is TemplateVariable variable)
            {
                _selectedIndex = _variables.IndexOf(variable);
                VariableSelected?.Invoke(this, variable.Variable);
                Hide(HiddenState.Close);
                OnClick(EventArgs.Empty);
            }
            e.Handled = true;
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Hide(HiddenState.Cancel);
            e.Handled = true;
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Hide(HiddenState.Cancel);
        }
    }
}