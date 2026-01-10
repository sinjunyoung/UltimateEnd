using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Scraper
{
    public class ScreenScraperCache
    {
        private const string ScreenScraperCacheDirectory = "ScreenScraperCache";
        private const string FAILED_MARKER = "__SCRAPER_FAILED__";
        private static readonly SemaphoreSlim _fileLock = new(1, 1);

        private const int SuccessCacheExpiryDays = 30;
        private const int FailedCacheExpiryDays = 90;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static string CacheDirectory
        {
            get
            {
                var provider = AppBaseFolderProviderFactory.Create?.Invoke();

                if (provider != null)
                    return Path.Combine(provider.GetAppBaseFolder(), ScreenScraperCacheDirectory);

                return Path.Combine(AppContext.BaseDirectory, ScreenScraperCacheDirectory);
            }
        }

        static ScreenScraperCache() => Directory.CreateDirectory(CacheDirectory);

        public static async Task<GameResult?> GetCachedResultAsync(string cacheKey)
        {
            try
            {
                var cacheFile = GetCacheFilePath(cacheKey);

                if (!File.Exists(cacheFile))
                    return null;

                await _fileLock.WaitAsync();

                try
                {
                    var json = await File.ReadAllTextAsync(cacheFile);
                    var result = JsonSerializer.Deserialize<GameResult>(json);

                    var isFailed = result?.Title == FAILED_MARKER;
                    var fileInfo = new FileInfo(cacheFile);
                    var expiryDays = isFailed ? FailedCacheExpiryDays : SuccessCacheExpiryDays;

                    if ((DateTime.Now - fileInfo.LastWriteTime).TotalDays > expiryDays)
                    {
                        try
                        {
                            File.Delete(cacheFile);
                        }
                        catch { }

                        return null;
                    }

                    return result;
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task SaveCachedResultAsync(string cacheKey, GameResult game)
        {
            try
            {
                var cacheFile = GetCacheFilePath(cacheKey);
                var json = JsonSerializer.Serialize(game, JsonOptions);

                await _fileLock.WaitAsync();

                try
                {
                    await File.WriteAllTextAsync(cacheFile, json);
                }
                finally
                {
                    _fileLock.Release();
                }
            }
            catch { }
        }

        public static async Task SaveFailedResultAsync(string cacheKey)
        {
            try
            {
                var failedMarker = new GameResult
                {
                    Title = FAILED_MARKER,
                    Description = "이전에 검색 실패한 항목입니다."
                };

                await SaveCachedResultAsync(cacheKey, failedMarker);
            }
            catch { }
        }

        public static async Task<bool> IsFailedResultAsync(string cacheKey)
        {
            try
            {
                var cached = await GetCachedResultAsync(cacheKey);
                return cached?.Title == FAILED_MARKER;
            }
            catch
            {
                return false;
            }
        }

        public static void ClearCache()
        {
            try
            {
                if (Directory.Exists(CacheDirectory))
                {
                    Directory.Delete(CacheDirectory, true);
                    Directory.CreateDirectory(CacheDirectory);
                }
            }
            catch { }
        }

        private static string GetCacheFilePath(string cacheKey)
        {
            var sanitizedKey = SanitizeCacheKey(cacheKey);
            return Path.Combine(CacheDirectory, $"{sanitizedKey}.json");
        }

        private static string SanitizeCacheKey(string cacheKey)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", cacheKey.Split(invalidChars));

            if (sanitized.Length > 200)
                sanitized = sanitized.Substring(0, 200);

            return sanitized;
        }
    }
}