using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class BoolToToggleAlignmentConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOn && isOn)
                return HorizontalAlignment.Right;

            return HorizontalAlignment.Left;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}