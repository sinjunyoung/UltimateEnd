using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class EmptyStringConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count > 0 && values[0] is string str)
                return string.IsNullOrEmpty(str) ? "(장르 없음)" : str;

            return "(장르 없음)";
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}