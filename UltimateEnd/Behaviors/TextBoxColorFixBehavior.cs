using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Xaml.Interactivity;
using UltimateEnd.Enums;
using UltimateEnd.Services;

namespace UltimateEnd.Behaviors
{
    public class TextBoxColorFixBehavior : Behavior<TextBox>
    {
        private static IBrush? _cachedForeground;
        private static IBrush? _cachedCaretBrush;
        private static bool _colorsLoaded = false;

        protected override void OnAttached()
        {
            base.OnAttached();
            if (AssociatedObject != null)
            {
                if (_colorsLoaded)
                    ApplyCachedColors();
                else
                    AssociatedObject.Loaded += OnLoaded;

                ThemeService.ThemeChanged += OnThemeChanged;
            }
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnLoaded;
                ThemeService.ThemeChanged -= OnThemeChanged;
            }
        }

        private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!_colorsLoaded)
                LoadColors();

            ApplyCachedColors();
        }

        private void OnThemeChanged(string theme)
        {
            LoadColors();
            ApplyCachedColors();
        }

        private static void LoadColors()
        {
            if (Application.Current?.TryGetResource("TextBox.Foreground", out var foregroundResource) == true && foregroundResource is IBrush foreground)
                _cachedForeground = foreground;

            if (Application.Current?.TryGetResource("TextBox.CaretBrush", out var caretResource) == true && caretResource is IBrush caretBrush)
                _cachedCaretBrush = caretBrush;

            _colorsLoaded = true;
        }

        private void ApplyCachedColors()
        {
            if (AssociatedObject == null) return;

            if (_cachedForeground != null)
                AssociatedObject.Foreground = _cachedForeground;

            if (_cachedCaretBrush != null)
                AssociatedObject.CaretBrush = _cachedCaretBrush;
        }

        private void ApplyColors()
        {
            if (AssociatedObject == null) return;

            if (Application.Current?.TryGetResource("TextBox.Foreground", out var foregroundResource) == true && foregroundResource is IBrush foreground)
                AssociatedObject.Foreground = foreground;

            if (Application.Current?.TryGetResource("TextBox.CaretBrush", out var caretResource) == true && caretResource is IBrush caretBrush)
                AssociatedObject.CaretBrush = caretBrush;
        }
    }
}