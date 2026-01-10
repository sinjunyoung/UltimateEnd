using System.Collections.Generic;
using System.Text.Json.Serialization;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Models
{
    public class PlatformMapping : IPlatformMapping
    {
        [JsonPropertyName("emulators")]
        public List<string> Emulators { get; set; } = new();

        [JsonPropertyName("default")]
        public string? Default { get; set; }
    }
}