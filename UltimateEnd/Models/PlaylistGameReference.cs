using System;
using System.Text.Json.Serialization;
using UltimateEnd.Services;

namespace UltimateEnd.Models
{
    public class PlaylistGameReference
    {
        [JsonIgnore]
        private readonly IPathConverter? _pathConverter = PathConverterFactory.Create?.Invoke();

        public string PlatformId { get; set; } = string.Empty;

        [JsonIgnore]
        public string BasePath { get; set; } = string.Empty;

        [JsonPropertyName("BasePath")]
        public string BasePathSerialized
        {
            get
            {
                if (_pathConverter != null && !string.IsNullOrEmpty(BasePath))
                    return _pathConverter.RealPathToFriendlyPath(BasePath);

                return BasePath;
            }
            set
            {
                if (_pathConverter != null && !string.IsNullOrEmpty(value))
                    BasePath = _pathConverter.FriendlyPathToRealPath(value);
                else
                    BasePath = value;
            }
        }

        public string RomFile { get; set; } = string.Empty;

        public int Order { get; set; }

        public string? Notes { get; set; }
    }
}