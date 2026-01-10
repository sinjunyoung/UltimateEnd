using Avalonia.Media.Imaging;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using UltimateEnd.Enums;

namespace UltimateEnd.Models
{
    public class PlatformInfo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonIgnore]
        public Bitmap Image { get; set; }

        [JsonPropertyName("aliases")]
        public List<string> Aliases { get; set; } = [];

        [JsonPropertyName("scraperId")]

        public ScreenScraperSystemId ScreenScraperSystemId { get; set; } = ScreenScraperSystemId.NotSupported;

        [JsonPropertyName("extensions")]
        public List<string> Extensions { get; set; } = [];

        public override string ToString()
        {
            return $"{Id}/{DisplayName}";
        }
    }
}