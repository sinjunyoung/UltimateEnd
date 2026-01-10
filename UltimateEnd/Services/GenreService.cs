using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace UltimateEnd.Services
{
    public class GenreService
    {
        private const string GenreFileName = "genres.json";
        private static Dictionary<int, string>? _cachedGenres;

        private static string GetGenreFilePath()
        {
            var provider = AppBaseFolderProviderFactory.Create?.Invoke();
            if (provider != null)
            {
                var directory = provider.GetAppBaseFolder();
                return Path.Combine(directory!, GenreFileName);
            }
            return Path.Combine(AppContext.BaseDirectory, GenreFileName);
        }

        public static Dictionary<int, string> LoadGenres()
        {
            if (_cachedGenres != null)
                return _cachedGenres;

            var filePath = GetGenreFilePath();

            if (!File.Exists(filePath))
            {
                // ScreenScraperGenre의 기본 장르 맵을 사용
                _cachedGenres = new Dictionary<int, string>(UltimateEnd.Scraper.Models.ScreenScraperGenre.GenreMap);
                SaveGenres(_cachedGenres);
                return _cachedGenres;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<int, string>>(json);
                _cachedGenres = loaded ?? new Dictionary<int, string>();
                return _cachedGenres;
            }
            catch
            {
                _cachedGenres = new Dictionary<int, string>(UltimateEnd.Scraper.Models.ScreenScraperGenre.GenreMap);
                return _cachedGenres;
            }
        }

        public static void SaveGenres(Dictionary<int, string> genres)
        {
            _cachedGenres = genres;

            try
            {
                var filePath = GetGenreFilePath();
                var directory = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(genres, options);
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public static void ClearCache()
        {
            _cachedGenres = null;
        }

        public static string GetGenreName(int id)
        {
            var genres = LoadGenres();
            return genres.TryGetValue(id, out var name)
                ? name
                : UltimateEnd.Scraper.Models.ScreenScraperGenre.UnknownGenreKorean;
        }

        public static int GetNextAvailableId()
        {
            var genres = LoadGenres();
            if (genres.Count == 0)
                return 100000; // 사용자 정의 장르는 100000부터 시작

            return genres.Keys.Max() + 1;
        }
    }
}