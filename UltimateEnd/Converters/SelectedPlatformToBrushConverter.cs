using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class SelectedPlatformToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string selectedId && parameter is string currentId)
            {
                if (selectedId == currentId)
                    return Brushes.White;
                else
                    return new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}