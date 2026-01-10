using System.Text.Json.Serialization;

namespace UltimateEnd.SaveFile
{
    public class GoogleDriveFile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("modifiedTime")]
        public string ModifiedTime { get; set; }
    }
}