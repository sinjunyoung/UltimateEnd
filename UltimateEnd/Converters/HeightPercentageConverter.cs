using Avalonia.Data.Converters;
using System;
using System.Globalization;
namespace UltimateEnd.Converters
{
    public class HeightPercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double height && double.TryParse(parameter?.ToString(), out double percent))
                return height * percent;

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}