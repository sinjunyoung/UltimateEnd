using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class IconKeyToGeometryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string iconKey && !string.IsNullOrEmpty(iconKey))
                if (Avalonia.Application.Current.TryGetResource(iconKey, null, out var resource)) return resource;

            return Avalonia.Application.Current.FindResource("Icon.SaveFile");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}