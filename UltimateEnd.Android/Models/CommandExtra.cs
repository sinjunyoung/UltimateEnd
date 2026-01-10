using System.Text.Json.Serialization;

namespace UltimateEnd.Android.Models
{
    public class CommandExtra
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ExtraType Type { get; set; } = ExtraType.String;
    }
}