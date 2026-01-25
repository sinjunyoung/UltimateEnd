using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace UltimateEnd.Converters
{
    public class BoolToFontSizeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSimpleMode && isSimpleMode)
            {
                if(Avalonia.Application.Current?.Resources.TryGetResource("FontSize.ListGameTitleBig", Avalonia.Application.Current?.ActualThemeVariant, out var big) == true)
                    return big;

                return 22;
            }

            if (Avalonia.Application.Current?.Resources.TryGetResource("FontSize.ListGameTitle", Avalonia.Application.Current?.ActualThemeVariant, out var resource) == true)
                return resource;

            return 16;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}