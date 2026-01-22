using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class SteamMetadataService
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly string _cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltimateEnd", "Cache", "Steam");        
        private static SteamMetadataService? _instance;
        private static readonly Lock _lock = new();

        public static SteamMetadataService Instance
        {
            get
            {
                if (_instance == null)
                    lock (_lock) _instance ??= new SteamMetadataService();

                return _instance;
            }
        }

        private SteamMetadataService()
        {
            if (!Directory.Exists(_cacheDirectory)) Directory.CreateDirectory(_cacheDirectory);
        }

        public static bool TryLoadFromCache(string appId, GameMetadata game)
        {
            var cachePath = Path.Combine(_cacheDirectory, $"{appId}.json");

            if (!File.Exists(cachePath)) return false;

            try
            {
                var json = File.ReadAllText(cachePath);
                var jsonDoc = JsonDocument.Parse(json);

                if (!jsonDoc.RootElement.TryGetProperty(appId, out var appElement)) return false;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var appDetails = JsonSerializer.Deserialize<SteamAppDetails>(appElement.GetRawText(), options);

                if (appDetails?.Success == true && appDetails.Data != null)
                {
                    ApplyApiMetadata(game, appDetails.Data, appId);

                    return true;
                }
            }
            catch
            {
                try { File.Delete(cachePath); } catch { }
            }

            return false;
        }

        public static async Task<bool> FetchMetadataAsync(string appId, GameMetadata game)
        {
            try
            {
                var url = $"https://store.steampowered.com/api/appdetails/?appids={appId}&l=korean&cc=kr";
                var response = await _httpClient.GetStringAsync(url);

                await SaveToCacheAsync(appId, response);

                var jsonDoc = JsonDocument.Parse(response);

                if (!jsonDoc.RootElement.TryGetProperty(appId, out var appElement)) return false;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var appDetails = JsonSerializer.Deserialize<SteamAppDetails>(appElement.GetRawText(), options);

                if (appDetails?.Success == true && appDetails.Data != null)
                {
                    await ApplyApiMetadataWithDownloadAsync(game, appDetails.Data, appId);

                    return true;
                }
            }
            catch { }

            return false;
        }

        private static void ApplyApiMetadata(GameMetadata game, SteamAppData data, string appId)
        {
            if (string.IsNullOrEmpty(game.Title) && !string.IsNullOrEmpty(data.Name)) game.Title = data.Name;

            if (string.IsNullOrEmpty(game.Description))
            {
                if (!string.IsNullOrEmpty(data.ShortDescription)) game.Description = data.ShortDescription;
                else if (!string.IsNullOrEmpty(data.AboutTheGame)) game.Description = StripHtmlTags(data.AboutTheGame);
            }

            if (string.IsNullOrEmpty(game.Developer) && data.Developers != null && data.Developers.Length > 0) game.Developer = string.Join(", ", data.Developers);

            if (string.IsNullOrEmpty(game.Genre) && data.Genres != null && data.Genres.Length > 0)
            {
                var genres = new List<string>();

                foreach (var genre in data.Genres)
                    if (!string.IsNullOrEmpty(genre.Description)) genres.Add(genre.Description);

                if (genres.Count > 0) game.Genre = string.Join(", ", genres);
            }

            var basePath = game.GetBasePath();

            var coverImagePath = Path.Combine(basePath, "covers", $"{appId}.jpg");

            if (File.Exists(coverImagePath)) game.CoverImagePath = coverImagePath;

            var logoImagePath = Path.Combine(basePath, "logos", $"{appId}.jpg");

            if (File.Exists(logoImagePath)) game.LogoImagePath = logoImagePath;

            var videoPath = Path.Combine(basePath, "videos", $"{appId}.webm");

            if (File.Exists(videoPath)) game.VideoPath = videoPath;
        }

        private static async Task ApplyApiMetadataWithDownloadAsync(GameMetadata game, SteamAppData data, string appId)
        {
            if (!string.IsNullOrEmpty(data.Name)) game.Title = data.Name;

            if (string.IsNullOrEmpty(game.Description))
            {
                if (!string.IsNullOrEmpty(data.ShortDescription)) game.Description = data.ShortDescription;
                else if (!string.IsNullOrEmpty(data.AboutTheGame)) game.Description = StripHtmlTags(data.AboutTheGame);
            }

            if (string.IsNullOrEmpty(game.Developer) && data.Developers != null && data.Developers.Length > 0)
                game.Developer = string.Join(", ", data.Developers);

            if (string.IsNullOrEmpty(game.Genre) && data.Genres != null && data.Genres.Length > 0)
            {
                var genres = new List<string>();

                foreach (var genre in data.Genres)
                    if (!string.IsNullOrEmpty(genre.Description)) genres.Add(genre.Description);

                if (genres.Count > 0) game.Genre = string.Join(", ", genres);
            }

            if (string.IsNullOrEmpty(game.LogoImagePath) && !string.IsNullOrEmpty(data.HeaderImage))
            {
                var logoPath = await DownloadImageAsync(data.HeaderImage, appId, "logo");

                if (!string.IsNullOrEmpty(logoPath)) game.LogoImagePath = logoPath;
            }

            if (string.IsNullOrEmpty(game.CoverImagePath) && data.Screenshots != null && data.Screenshots.Length > 0)
            {
                var firstScreenshot = data.Screenshots[0];

                if (!string.IsNullOrEmpty(firstScreenshot.PathFull))
                {
                    var coverPath = await DownloadImageAsync(firstScreenshot.PathFull, appId, "cover");

                    if (!string.IsNullOrEmpty(coverPath)) game.CoverImagePath = coverPath;
                }
            }

            if (string.IsNullOrEmpty(game.VideoPath) && data.Movies != null && data.Movies.Length > 0)
            {
                var firstMovie = data.Movies[0];
                string? videoUrl = firstMovie.Webm?.Quality480 ?? firstMovie.Mp4?.Quality480;

                if (!string.IsNullOrEmpty(videoUrl) && !videoUrl.Contains(".m3u8"))
                {
                    var localPath = await DownloadVideoAsync(videoUrl, appId);

                    if (!string.IsNullOrEmpty(localPath)) game.VideoPath = localPath;
                }
            }
        }

        private static async Task<string?> DownloadImageAsync(string url, string appId, string suffix)
        {
            try
            {
                var extension = Path.GetExtension(url).Split('?')[0];

                if (string.IsNullOrEmpty(extension)) extension = ".jpg";

                var steamBasePath = Path.Combine(AppSettings.SystemAppsPath, "steam");
                string folderName = suffix == "cover" ? "covers" : "logos";
                var targetFolder = Path.Combine(steamBasePath, folderName);

                if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

                var fileName = $"{appId}{extension}";
                var localPath = Path.Combine(targetFolder, fileName);

                if (File.Exists(localPath)) return localPath;

                var imageData = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, imageData);

                return localPath;
            }
            catch
            {
                return null;
            }
        }

        private static async Task<string?> DownloadVideoAsync(string url, string appId)
        {
            try
            {
                var extension = url.Contains(".webm") ? ".webm" : ".mp4";
                var steamBasePath = Path.Combine(AppSettings.SystemAppsPath, "steam");
                var videosFolder = Path.Combine(steamBasePath, "videos");

                if (!Directory.Exists(videosFolder)) Directory.CreateDirectory(videosFolder);

                var fileName = $"{appId}{extension}";
                var localPath = Path.Combine(videosFolder, fileName);

                if (File.Exists(localPath)) return localPath;

                var videoData = await _httpClient.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(localPath, videoData);

                return localPath;
            }
            catch
            {
                return null;
            }
        }

        private static string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;

            var result = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            result = System.Net.WebUtility.HtmlDecode(result);

            return result.Trim();
        }

        private static async Task SaveToCacheAsync(string appId, string json)
        {
            try
            {
                var cachePath = Path.Combine(_cacheDirectory, $"{appId}.json");

                await File.WriteAllTextAsync(cachePath, json);
            }
            catch { }
        }

        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(_cacheDirectory))
                    foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
                        try { File.Delete(file); } catch { }
            }
            catch { }
        }
    }

    #region Steam API 데이터 모델

    public class SteamAppDetails
    {
        public bool Success { get; set; }

        public SteamAppData? Data { get; set; }
    }

    public class SteamAppData
    {
        public string? Name { get; set; }

        [JsonPropertyName("short_description")]
        public string? ShortDescription { get; set; }

        [JsonPropertyName("about_the_game")]
        public string? AboutTheGame { get; set; }

        [JsonPropertyName("header_image")]
        public string? HeaderImage { get; set; }

        public string? Background { get; set; }

        public string[]? Developers { get; set; }

        public string[]? Publishers { get; set; }

        public SteamGenre[]? Genres { get; set; }

        public SteamScreenshot[]? Screenshots { get; set; }

        public SteamMovie[]? Movies { get; set; }
    }

    public class SteamGenre
    {
        public string? Description { get; set; }
    }

    public class SteamScreenshot
    {
        [JsonPropertyName("path_thumbnail")]
        public string? PathThumbnail { get; set; }

        [JsonPropertyName("path_full")]
        public string? PathFull { get; set; }
    }

    public class SteamMovie
    {
        public string? Name { get; set; }

        public string? Thumbnail { get; set; }

        [JsonPropertyName("webm")]
        public SteamMovieWebm? Webm { get; set; }

        [JsonPropertyName("mp4")]
        public SteamMovieMp4? Mp4 { get; set; }
    }

    public class SteamMovieWebm
    {
        [JsonPropertyName("480")]
        public string? Quality480 { get; set; }
    }

    public class SteamMovieMp4
    {
        [JsonPropertyName("480")]
        public string? Quality480 { get; set; }
    }

    public class SteamUserData
    {
        public bool IsFavorite { get; set; }

        public bool Ignore { get; set; }

        public bool HasKorean { get; set; }

        public string? CustomTitle { get; set; }

        public string? CustomDescription { get; set; }

        public string? CustomDeveloper { get; set; }

        public string? CustomGenre { get; set; }

        public string? EmulatorId { get; set; }
    }

    #endregion

    public static class GameMetadataExtensions
    {
        public static bool IsSteamGame(this GameMetadata game) => game.PlatformId == "steam";

        public static string GetSteamAppId(this GameMetadata game) => game.RomFile;
    }
}

