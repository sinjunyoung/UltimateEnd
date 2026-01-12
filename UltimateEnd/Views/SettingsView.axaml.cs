using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                this.Focus();
                UpdateThemeBorders();
            };
        }

        private void OnThemeItemClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ThemeOption theme)
            {
                if (DataContext is SettingsViewModel vm)
                {
                    vm.SelectTheme(theme);
                    UpdateThemeBorders();
                }
            }
        }

        private void UpdateThemeBorders()
        {
            if (DataContext is not SettingsViewModel vm) return;

            var scrollViewer = this.GetVisualDescendants()
                .OfType<ScrollViewer>()
                .FirstOrDefault();

            if (scrollViewer == null) return;

            var borders = scrollViewer.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Name == "ThemeItemBorder");

            foreach (var border in borders)
            {
                if (border.DataContext is ThemeOption themeOption)
                {
                    if (themeOption.IsSelected)
                    {
                        border.BorderThickness = new Thickness(2);
                        border.BorderBrush = this.FindResource("Accent.Blue") as IBrush;
                    }
                    else
                    {
                        border.BorderThickness = new Thickness(0);
                        border.BorderBrush = Brushes.Transparent;
                    }
                }
            }
        }

        private async void OnBackClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
            {
                await WavSounds.Cancel();
                vm.RequestBack();
            }
        }

        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB) || e.Key == Key.Back)
            {
                if (DataContext is SettingsViewModel vm)
                {
                    await WavSounds.Cancel();
                    vm.RequestBack();
                    e.Handled = true;
                }
            }
        }
    }
}