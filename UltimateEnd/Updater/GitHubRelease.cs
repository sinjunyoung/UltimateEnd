using System.Text.Json.Serialization;

namespace UltimateEnd.Updater
{
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; }
    }
}