using Avalonia.Media.Imaging;

namespace UltimateEnd.Models
{
    public class PlatformOption
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public Bitmap Image { get; set; } = null;
    }
}