using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UltimateEnd.Models
{
    public class PlatformDatabase
    {
        [JsonPropertyName("platforms")]
        public List<PlatformInfo> Platforms { get; set; } = [];
    }
}