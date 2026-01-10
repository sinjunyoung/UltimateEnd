using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Globalization;

namespace UltimateEnd.Converters
{ 
    public class BoolToHighlightBrushConverter : IValueConverter
    {
        private static IBrush GetBrush(string resourceKey)
        {
            if (Application.Current!.TryGetResource(resourceKey, ThemeVariant.Default, out object? value) && value is IBrush brush)
                return brush;

            return Brushes.Transparent;
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
                return GetBrush("Background.Hover");

            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}