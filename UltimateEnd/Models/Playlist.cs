using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using UltimateEnd.Managers;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Models
{
    public class Playlist
    {
        [JsonIgnore]
        private readonly IPathConverter? _pathConverter = PathConverterFactory.Create?.Invoke();

        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = string.Empty;

        [JsonIgnore]
        public string? CoverImagePath { get; set; }

        [JsonPropertyName("CoverImagePath")]
        public string? CoverImagePathSerialized
        {
            get
            {
                if (_pathConverter != null && !string.IsNullOrEmpty(CoverImagePath))
                    return _pathConverter.RealPathToFriendlyPath(CoverImagePath);

                return CoverImagePath;
            }
            set
            {
                if (_pathConverter != null && !string.IsNullOrEmpty(value))
                    CoverImagePath = _pathConverter.FriendlyPathToRealPath(value);
                else
                    CoverImagePath = value;
            }
        }

        public List<PlaylistGameReference> GameReferences { get; set; } = [];

        [JsonIgnore]
        public int GameCount => GameReferences.Count;

        public Platform ToPlatform()
        {
            var hasValidCover = !string.IsNullOrEmpty(CoverImagePath) && File.Exists(CoverImagePath);
            var coverImage = hasValidCover ? CoverImagePath : ResourceHelper.GetPlatformImage("playlist");
            var logoImage = hasValidCover ? CoverImagePath : ResourceHelper.GetLogoImage("playlist");

            return new Platform
            {
                Id = PlaylistManager.GetPlaylistPlatformId(this.Id),
                Name = Name,
                ImagePath = coverImage,
                LogoPath = logoImage,
                IsPlaylist = true
            };
        }
    }
}