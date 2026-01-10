using System.Text.Json.Serialization;

namespace UltimateEnd.Models
{
    public class StoredTokens
    {
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
    }
}