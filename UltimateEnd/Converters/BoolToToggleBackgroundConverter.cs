using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class BoolToToggleBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOn && isOn)
                return new SolidColorBrush(Color.Parse("#4CAF50"));

            return new SolidColorBrush(Color.Parse("#9E9E9E"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}