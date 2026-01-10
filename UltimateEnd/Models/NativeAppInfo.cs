namespace UltimateEnd.Models
{
    public class NativeAppInfo
    {
        public string Identifier { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string ActivityName { get; set; } = string.Empty;

        public Avalonia.Media.Imaging.Bitmap? Icon { get; set; }
    }
}