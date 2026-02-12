using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Scraper
{
    public class ScreenScraperCache
    {
        private const string CACHE_FILE_NAME = "screenscraper_cache.ue";
        private const string FAILED_MARKER = "__SCRAPER_FAILED__";
        private static readonly SemaphoreSlim _fileLock = new(1, 1);
        private static readonly SemaphoreSlim _loadLock = new(1, 1);

        private const int SuccessCacheExpiryDays = 30;
        private const int FailedCacheExpiryDays = 90;

        private static Dictionary<string, CachedEntry> _cache = [];
        private static bool _isLoaded = false;
        private static bool _isDirty = false;
        private static readonly Timer? _autoSaveTimer = new(async _ => await AutoSaveAsync(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)); 

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static string CacheFilePath
        {
            get
            {
                var provider = AppBaseFolderProviderFactory.Create?.Invoke();
                var baseFolder = provider?.GetSettingsFolder() ?? AppContext.BaseDirectory;

                return Path.Combine(baseFolder, CACHE_FILE_NAME);
            }
        }

        public static async Task InitializeAsync()
        {
            try
            {
                await LoadCacheAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cache] 초기 로드 실패: {ex.Message}");
            }
        }

        private static async Task LoadCacheAsync()
        {
            if (_isLoaded) return;

            await _loadLock.WaitAsync();

            try
            {
                if (_isLoaded) return;

                if (File.Exists(CacheFilePath))
                {
                    await using var fileStream = File.OpenRead(CacheFilePath);
                    await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
                    var loadedCache = await JsonSerializer.DeserializeAsync<Dictionary<string, CachedEntry>>(gzipStream);

                    if (loadedCache != null)
                    {
                        var now = DateTime.Now;

                        foreach (var kvp in loadedCache)
                        {
                            var isFailed = kvp.Value.Title == FAILED_MARKER;
                            var expiryDays = isFailed ? FailedCacheExpiryDays : SuccessCacheExpiryDays;

                            if ((now - kvp.Value.CachedAt).TotalDays <= expiryDays) _cache[kvp.Key] = kvp.Value;
                        }
                    }
                }

                _isLoaded = true;
            }
            catch
            {
                _cache = [];
                _isLoaded = true;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public static async Task<GameResult?> GetCachedResultAsync(string cacheKey)
        {
            await EnsureLoadedAsync();

            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                var isFailed = entry.Title == FAILED_MARKER;
                var expiryDays = isFailed ? FailedCacheExpiryDays : SuccessCacheExpiryDays;

                if ((DateTime.Now - entry.CachedAt).TotalDays <= expiryDays) return ToGameResult(entry);

                _cache.Remove(cacheKey);
                _isDirty = true;
            }
            return null;
        }

        public static async Task SaveCachedResultAsync(string cacheKey, GameResult game, int systemId)
        {
            await EnsureLoadedAsync();

            _cache[cacheKey] = ToCachedEntry(game, systemId);
            _isDirty = true;
        }

        public static async Task SaveFailedResultAsync(string cacheKey)
        {
            await EnsureLoadedAsync();

            _cache[cacheKey] = new CachedEntry
            {
                Title = FAILED_MARKER,
                Description = "이전에 검색 실패한 항목입니다.",
                CachedAt = DateTime.Now
            };

            _isDirty = true;
        }

        public static async Task<bool> IsFailedResultAsync(string cacheKey)
        {
            await EnsureLoadedAsync();

            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                var isFailed = entry.Title == FAILED_MARKER;
                var expiryDays = isFailed ? FailedCacheExpiryDays : SuccessCacheExpiryDays;

                if ((DateTime.Now - entry.CachedAt).TotalDays <= expiryDays) return isFailed;

                _cache.Remove(cacheKey);
                _isDirty = true;
            }

            return false;
        }

        public static async Task FlushAsync()
        {
            if (!_isDirty) return;

            await _fileLock.WaitAsync();
            try
            {
                await using var fileStream = File.Create(CacheFilePath);
                await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);

                await JsonSerializer.SerializeAsync(gzipStream, _cache, JsonOptions);

                _isDirty = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cache] 저장 실패: {ex.Message}");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private static async Task AutoSaveAsync()
        {
            if (_isDirty) await FlushAsync();
        }

        public static void ClearCache()
        {
            try
            {
                _cache.Clear();
                _isDirty = true;

                if (File.Exists(CacheFilePath)) File.Delete(CacheFilePath);
            }
            catch { }
        }
        
        private static async Task EnsureLoadedAsync()
        {
            if (_isLoaded) return;

            await LoadCacheAsync();
        }

        public static void Shutdown()
        {
            _autoSaveTimer?.Dispose();

            if (_isDirty)
            {
                try
                {
                    FlushSync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Cache] Shutdown 저장 실패: {ex.Message}");
                }
            }

            _isLoaded = false;
        }

        public static void FlushSync()
        {
            if (!_isDirty) return;

            _fileLock.Wait();

            try
            {
                using var fileStream = File.Create(CacheFilePath);
                using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);

                JsonSerializer.Serialize(gzipStream, _cache, JsonOptions);

                _isDirty = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cache] 저장 실패: {ex.Message}");
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private static CachedEntry ToCachedEntry(GameResult game, int systemId)
        {
            var entry = new CachedEntry
            {
                GameId = game.Id,
                SystemId = systemId,
                Title = game.Title,
                Description = game.Description,
                ReleaseDate = game.ReleaseDate,
                Developer = game.Developer,
                Publisher = game.Publisher,
                Genre = game.Genre,
                Players = game.Players,
                Rating = game.Rating,
                CachedAt = DateTime.Now
            };

            if (game.Media.TryGetValue("boxFront", out var boxFront))
            {
                entry.BoxFrontMedia = ExtractMediaParam(boxFront.Url);
                entry.BoxFrontFormat = boxFront.Format;
            }

            if (game.Media.TryGetValue("logo", out var logo))
            {
                entry.LogoMedia = ExtractMediaParam(logo.Url);
                entry.LogoFormat = logo.Format;
            }

            if (game.Media.TryGetValue("video", out var video))
            {
                entry.VideoMedia = ExtractMediaParam(video.Url);
                entry.VideoFormat = video.Format;
            }

            return entry;
        }

        private static GameResult ToGameResult(CachedEntry entry)
        {
            var game = new GameResult
            {
                Id = entry.GameId,
                Title = entry.Title,
                Description = entry.Description,
                ReleaseDate = entry.ReleaseDate,
                Developer = entry.Developer,
                Publisher = entry.Publisher,
                Genre = entry.Genre,
                Players = entry.Players,
                Rating = entry.Rating,
                Media = []
            };

            if (!string.IsNullOrEmpty(entry.BoxFrontMedia))
            {
                game.Media["boxFront"] = new MediaInfo
                {
                    Url = BuildMediaUrl(entry.SystemId, entry.GameId, entry.BoxFrontMedia),
                    Format = entry.BoxFrontFormat
                };
            }

            if (!string.IsNullOrEmpty(entry.LogoMedia))
            {
                game.Media["logo"] = new MediaInfo
                {
                    Url = BuildMediaUrl(entry.SystemId, entry.GameId, entry.LogoMedia),
                    Format = entry.LogoFormat
                };
            }

            if (!string.IsNullOrEmpty(entry.VideoMedia))
            {
                game.Media["video"] = new MediaInfo
                {
                    Url = BuildVideoUrl(entry.SystemId, entry.GameId, entry.VideoMedia),
                    Format = entry.VideoFormat
                };
            }

            return game;
        }

        private static string? ExtractMediaParam(string url)
        {
            var match = Regex.Match(url, @"media=([^&]+)");

            return match.Success ? match.Groups[1].Value : null;
        }

        private static string BuildMediaUrl(int systemId, int gameId, string mediaType)
        {
            var config = ScreenScraperConfig.Instance;

            return $"https://neoclone.screenscraper.fr/api2/mediaJeu.php?" +
                   $"devid={config.ApiDevU}&devpassword={config.ApiDevP}" +
                   $"&softname=UltimateEnd&ssid=&sspassword=" +
                   $"&systemeid={systemId}&jeuid={gameId}&media={mediaType}";
        }

        private static string BuildVideoUrl(int systemId, int gameId, string mediaType)
        {
            var config = ScreenScraperConfig.Instance;

            return $"https://neoclone.screenscraper.fr/api2/mediaVideoJeu.php?" +
                   $"devid={config.ApiDevU}&devpassword={config.ApiDevP}" +
                   $"&softname=UltimateEnd&ssid=&sspassword=" +
                   $"&systemeid={systemId}&jeuid={gameId}&media={mediaType}";
        }
    }
}