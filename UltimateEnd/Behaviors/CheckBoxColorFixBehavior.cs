using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using UltimateEnd.Enums;
using UltimateEnd.Services;

namespace UltimateEnd.Behaviors
{
    public class CheckBoxColorFixBehavior : Behavior<CheckBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();

            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded += OnLoaded;
                AssociatedObject.PointerEntered += OnPointerEntered;
                AssociatedObject.PointerExited += OnPointerExited;
                AssociatedObject.PropertyChanged += OnPropertyChanged;
                ThemeService.ThemeChanged += OnThemeChanged;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnLoaded;
                AssociatedObject.PointerEntered -= OnPointerEntered;
                AssociatedObject.PointerExited -= OnPointerExited;
                AssociatedObject.PropertyChanged -= OnPropertyChanged;
                ThemeService.ThemeChanged -= OnThemeChanged;
            }
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ApplyColors();

        private void OnThemeChanged(string theme) => ApplyColors();

        private void OnPointerEntered(object? sender, PointerEventArgs e) => ApplyColors();

        private void OnPointerExited(object? sender, PointerEventArgs e) => ApplyColors();

        private void OnPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "IsChecked")
                ApplyColors();
        }

        private void ApplyColors()
        {
            if (AssociatedObject == null) return;

            var foreground = GetBrush("CheckBox.Foreground");

            if (foreground != null)
                AssociatedObject.Foreground = foreground;

            ForceApplyColors(AssociatedObject, foreground);
        }

        private void ForceApplyColors(Visual visual, IBrush? foreground)
        {
            foreach (var child in visual.GetVisualChildren())
            {
                if (child is ContentPresenter presenter && foreground != null)
                    presenter.Foreground = foreground;

                if (child is TextBlock textBlock && foreground != null)
                    textBlock.Foreground = foreground;

                if (child is Border border && border.Name != "FocusVisual")
                {
                    if (AssociatedObject.IsChecked != true)
                    {
                        var boxBorder = GetBrush("CheckBox.Box.BorderBrush");
                        if (boxBorder != null)
                            border.BorderBrush = boxBorder;
                    }
                }

                ForceApplyColors(child, foreground);
            }
        }

        private IBrush? GetBrush(string key)
        {
            if (Application.Current?.TryGetResource(key, out var resource) == true && resource is IBrush brush)
                return brush;

            return null;
        }
    }
}