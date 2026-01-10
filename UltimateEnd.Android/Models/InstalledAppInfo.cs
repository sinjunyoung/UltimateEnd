using Avalonia.Media.Imaging;

namespace UltimateEnd.Android.Models
{
    public class InstalledAppInfo
    {
        public string DisplayName { get; set; } = string.Empty;

        public string PackageName { get; set; } = string.Empty;

        public string ActivityName { get; set; } = string.Empty;

        public Bitmap? Icon { get; set; }
    }
}