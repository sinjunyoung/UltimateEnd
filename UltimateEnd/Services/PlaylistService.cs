using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class PlaylistService
    {
        private const string PlaylistsFileName = "playlists.json";

        private static string PlaylistsDirectory => Path.Combine(AppBaseFolderProviderFactory.Create?.Invoke().GetPlatformsFolder(), "Playlists");

        public static List<Playlist> LoadPlaylists()
        {
            var filePath = Path.Combine(PlaylistsDirectory, PlaylistsFileName);

            if (!File.Exists(filePath)) return [];

            try
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<Playlist>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }

        public static void SavePlaylists(List<Playlist> playlists)
        {
            Directory.CreateDirectory(PlaylistsDirectory);

            var filePath = Path.Combine(PlaylistsDirectory, PlaylistsFileName);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var json = JsonSerializer.Serialize(playlists, options);

            File.WriteAllText(filePath, json);
        }
    }
}