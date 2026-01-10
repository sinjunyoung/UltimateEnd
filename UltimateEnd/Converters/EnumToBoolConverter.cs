using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class EnumToBoolConverter : IValueConverter
    {
        public static readonly EnumToBoolConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string enumValue = value.ToString();
            string targetValue = parameter.ToString();

            return string.Equals(enumValue, targetValue, StringComparison.OrdinalIgnoreCase);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue && parameter != null)
                return Enum.Parse(targetType, parameter.ToString());

            return Avalonia.Data.BindingOperations.DoNothing;
        }
    }
}