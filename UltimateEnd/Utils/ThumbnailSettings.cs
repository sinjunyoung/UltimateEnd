using Avalonia;
using UltimateEnd.Enums;

namespace UltimateEnd.Utils
{
    public static class ThumbnailSettings
    {
        public static GameViewMode GameViewMode { get; set; } = GameViewMode.List;

        public static int GetMaxCoverWidth()
        {
            if (GameViewMode == GameViewMode.List)
                return 9999;

            if (Application.Current?.Resources.TryGetResource("Size.CoverWidth", null, out var logoWidth) == true)
            {
                if (logoWidth is double width)
                    return (int)width;
            }

            return 300;
        }

        public static int GetMaxLogoWidth()
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource("Size.LogoWidth", null, out var logoWidth) == true)
                {
                    if (logoWidth is double width)
                        return (int)width;
                }
            }
            catch { }

            return GetMaxCoverWidth() / 2;
        }
    }
}