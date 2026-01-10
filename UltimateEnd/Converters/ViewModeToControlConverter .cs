using Avalonia.Data.Converters;
using System;
using System.Globalization;
using UltimateEnd.Enums;
using UltimateEnd.Views;

namespace UltimateEnd.Converters
{
    public class ViewModeToControlConverter : IValueConverter
    {
        public static ViewModeToControlConverter Instance { get; } = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is GameViewMode mode && mode == GameViewMode.Grid
                ? new GameGridView()
                : new GameListView();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}