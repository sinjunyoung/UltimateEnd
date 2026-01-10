using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class GenreComparisonConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
                return false;

            var gameGenre = values[0] as string;
            var selectedGenre = values[1] as string;

            if (string.IsNullOrEmpty(gameGenre) && string.IsNullOrEmpty(selectedGenre))
                return true;

            if (!string.IsNullOrEmpty(gameGenre) && !string.IsNullOrEmpty(selectedGenre))
                return string.Equals(gameGenre, selectedGenre, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}