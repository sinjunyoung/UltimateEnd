using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class MultiplyConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return false;

            var value1 = values[0];
            var value2 = values[1];

            if (value1 == null && value2 == null)
                return true;

            if (value1 == null || value2 == null)
                return false;

            return value1.Equals(value2);
        }
    }
}