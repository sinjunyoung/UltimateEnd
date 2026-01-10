using System.Collections.Generic;
using System.Text.Json.Serialization;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Models
{
    public class PlatformMapping : IPlatformMapping
    {
        [JsonPropertyName("emulators")]
        public List<string> Emulators { get; set; } = [];

        [JsonPropertyName("default")]
        public string? Default { get; set; }
    }
}