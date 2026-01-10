using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class FavoriteIconConverter : IValueConverter
    {
        public static readonly FavoriteIconConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value is bool isFavorite && isFavorite) ? "⭐" : "☆";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}