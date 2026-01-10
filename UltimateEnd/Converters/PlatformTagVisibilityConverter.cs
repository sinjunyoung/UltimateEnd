using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using UltimateEnd.Managers;

namespace UltimateEnd.Converters
{
    public class PlatformTagVisibilityConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
                return false;

            if (values[0] is not string gamePlatformId || string.IsNullOrWhiteSpace(gamePlatformId))
                return false;

            if (values[1] is not string currentPlatformId)
                return false;

            return GameMetadataManager.IsSpecialPlatform(currentPlatformId);
        }
    }
}