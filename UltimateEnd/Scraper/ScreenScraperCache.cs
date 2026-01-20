using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private const string CACHE_FILE_NAME = "screenscraper_cache.json";
        private const string FAILED_MARKER = "__SCRAPER_FAILED__";
        private static readonly SemaphoreSlim _fileLock = new(1, 1);

        private const int SuccessCacheExpiryDays = 30;
        private const int FailedCacheExpiryDays = 90;

        private static Dictionary<string, CachedEntry> _cache = [];
        private static bool _isLoaded = false;
        private static bool _isDirty = false;
        private static readonly Timer? _autoSaveTimer;

        private static readonly TaskCompletionSource<bool> _loadCompletionSource = new();

        private class CachedEntry
        {
            public GameResult Result { get; set; } = null!;
            public DateTime CachedAt { get; set; }
        }

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
                var baseFolder = provider?.GetAppBaseFolder() ?? AppContext.BaseDirectory;

                return Path.Combine(baseFolder, CACHE_FILE_NAME);
            }
        }

        static ScreenScraperCache()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadCacheAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Cache] 초기 로드 실패: {ex.Message}");
                }
            });

            _autoSaveTimer = new Timer(async _ => await AutoSaveAsync(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        private static async Task LoadCacheAsync()
        {
            if (_isLoaded) return;

            await _fileLock.WaitAsync();

            try
            {
                if (_isLoaded) return;

                if (File.Exists(CacheFilePath))
                {
                    var json = await File.ReadAllTextAsync(CacheFilePath);
                    var loadedCache = JsonSerializer.Deserialize<Dictionary<string, CachedEntry>>(json);

                    if (loadedCache != null)
                    {
                        var now = DateTime.Now;

                        foreach (var kvp in loadedCache)
                        {
                            var isFailed = kvp.Value.Result?.Title == FAILED_MARKER;
                            var expiryDays = isFailed ? FailedCacheExpiryDays : SuccessCacheExpiryDays;

                            if ((now - kvp.Value.CachedAt).TotalDays <= expiryDays) _cache[kvp.Key] = kvp.Value;
                        }
                    }
                }

                _isLoaded = true;
                _loadCompletionSource.TrySetResult(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Cache] 로드 실패: {ex.Message}");
                _cache = [];
                _isLoaded = true;
                _loadCompletionSource.TrySetResult(false);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public static async Task<GameResult?> GetCachedResultAsync(string cacheKey)
        {
            await EnsureLoadedAsync();

            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                var isFailed = entry.Result?.Title == FAILED_MARKER;
                var expiryDays = isFailed ? FailedCacheExpiryDays : SuccessCacheExpiryDays;

                if ((DateTime.Now - entry.CachedAt).TotalDays <= expiryDays) return entry.Result;

                _cache.Remove(cacheKey);
                _isDirty = true;
            }

            return null;
        }

        public static GameResult? GetCachedResult(string cacheKey)
        {
            if (!_isLoaded) return null;

            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                var isFailed = entry.Result?.Title == FAILED_MARKER;
                var expiryDays = isFailed ? FailedCacheExpiryDays : SuccessCacheExpiryDays;

                if ((DateTime.Now - entry.CachedAt).TotalDays <= expiryDays) return entry.Result;

                _cache.Remove(cacheKey);
                _isDirty = true;
            }

            return null;
        }

        public static async Task SaveCachedResultAsync(string cacheKey, GameResult game)
        {
            await EnsureLoadedAsync();

            _cache[cacheKey] = new CachedEntry
            {
                Result = game,
                CachedAt = DateTime.Now
            };

            _isDirty = true;
        }

        public static async Task SaveFailedResultAsync(string cacheKey)
        {
            var failedMarker = new GameResult
            {
                Title = FAILED_MARKER,
                Description = "이전에 검색 실패한 항목입니다."
            };

            await SaveCachedResultAsync(cacheKey, failedMarker);
        }

        public static bool IsFailedResult(string cacheKey)
        {
            if (!_isLoaded) return false;

            if (_cache.TryGetValue(cacheKey, out var entry))
            {
                var isFailed = entry.Result?.Title == FAILED_MARKER;
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
                var json = JsonSerializer.Serialize(_cache, JsonOptions);
                await File.WriteAllTextAsync(CacheFilePath, json);
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

            await _loadCompletionSource.Task;
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
        }

        public static void FlushSync()
        {
            if (!_isDirty) return;

            _fileLock.Wait();
            try
            {
                var json = JsonSerializer.Serialize(_cache, JsonOptions);
                File.WriteAllText(CacheFilePath, json);
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
    }
}