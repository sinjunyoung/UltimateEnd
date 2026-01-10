using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Managers
{
    public class AllGamesManager : IDisposable
    {
        private const int HistoryMaxCount = 200;
        private static AllGamesManager? _instance;
        private static readonly object _lock = new();
        private readonly Dictionary<string, GameMetadata> _allGames = [];
        private readonly object _gamesLock = new();
        private bool _isLoaded = false;
        private CancellationTokenSource? _fetchCancellationTokenSource;
        private bool _disposed;

        private readonly HashSet<string> _invalidatedPlatforms = [];

        public static AllGamesManager Instance
        {
            get
            {
                if (_instance == null) lock (_lock) _instance ??= new AllGamesManager();
                return _instance;
            }
        }

        private AllGamesManager() { }

        private void EnsureLoaded()
        {
            if (_isLoaded) return;

            lock (_gamesLock)
            {
                if (_isLoaded) return;

                _allGames.Clear();

                var settings = SettingsService.LoadSettings();

                if (settings.PlatformSettings == null)
                {
                    _isLoaded = true;
                    return;
                }

                var mappingConfig = PlatformMappingService.Instance.LoadMapping();
                var converter = PathConverterFactory.Create?.Invoke();

                foreach (var platform in settings.PlatformSettings)
                {
                    var compositeKey = platform.Key;
                    var realPath = converter?.FriendlyPathToRealPath(compositeKey) ?? compositeKey;

                    MetadataService.ScanRomsFolder(realPath);

                    var games = MetadataService.LoadMetadata(realPath);                    
                    var actualPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? PlatformInfoService.NormalizePlatformId(platform.Value.Name);

                    foreach (var game in games)
                    {
                        game.PlatformId = actualPlatformId;
                        game.SetBasePath(realPath);

                        var key = GetGameKey(compositeKey, game.RomFile);
                        _allGames[key] = game;
                    }
                }

                var systemAppsPath = AppSettings.SystemAppsPath;

                if (!string.IsNullOrEmpty(systemAppsPath))
                {
                    if (OperatingSystem.IsWindows()) LoadSteamGames(systemAppsPath);

                    var appProvider = AppProviderFactory.Create?.Invoke();

                    if (appProvider != null) LoadNativeApps(systemAppsPath, appProvider.PlatformId);
                }

                _isLoaded = true;
            }
        }

        private void LoadNativeApps(string systemAppsPath, string platformId)
        {
            try
            {
                var games = MetadataService.LoadMetadata(platformId, systemAppsPath);
                var realSystemAppsPath = systemAppsPath;
                var converter = PathConverterFactory.Create?.Invoke();

                if (converter != null)
                    realSystemAppsPath = converter.FriendlyPathToRealPath(systemAppsPath) ?? systemAppsPath;

                foreach (var game in games)
                {
                    game.PlatformId = platformId;
                    game.SetBasePath(Path.Combine(realSystemAppsPath, platformId));
                    var key = GetGameKey(platformId, game.RomFile);
                    _allGames[key] = game;
                }
            }
            catch { }
        }

        private void LoadSteamGames(string systemAppsPath)
        {
            try
            {
                _fetchCancellationTokenSource?.Cancel();
                _fetchCancellationTokenSource?.Dispose();
                _fetchCancellationTokenSource = new CancellationTokenSource();

                var scanner = new SteamGameScanner();
                var steamGames = SteamGameScanner.ScanSteamGames();

                if (steamGames.Count == 0) return;

                var savedMetadata = MetadataService.LoadMetadata(GameMetadataManager.SteamKey, systemAppsPath);

                foreach (var savedGame in savedMetadata) savedGame.PlatformId = GameMetadataManager.SteamKey;

                var metadataDict = savedMetadata.Count > 0 ? savedMetadata.ToDictionary(g => g.RomFile) : null;
                var metadataService = SteamMetadataService.Instance;
                var gamesToFetch = new List<(string appId, GameMetadata game)>();

                foreach (var game in steamGames)
                {
                    var appId = Path.GetFileNameWithoutExtension(game.RomFile);

                    if (metadataDict != null && metadataDict.TryGetValue(game.RomFile, out var savedGame))
                    {
                        var key = GetGameKey(GameMetadataManager.SteamKey, game.RomFile);
                        _allGames[key] = savedGame;
                    }
                    else
                    {
                        metadataService.TryLoadFromCache(appId, game);

                        var key = GetGameKey(GameMetadataManager.SteamKey, game.RomFile);
                        _allGames[key] = game;

                        gamesToFetch.Add((appId, game));
                    }
                }

                if (gamesToFetch.Count > 0)
                    _ = FetchSteamMetadataInBackgroundAsync(gamesToFetch, _fetchCancellationTokenSource.Token);
            }
            catch { }
        }

        private static async Task FetchSteamMetadataInBackgroundAsync(List<(string appId, GameMetadata game)> gamesToFetch, CancellationToken cancellationToken)
        {
            var metadataService = SteamMetadataService.Instance;

            foreach (var (appId, game) in gamesToFetch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltimateEnd", "Cache", "Steam");
                    var jsonPath = Path.Combine(cacheDir, $"{appId}.json");
                    var headerPath = Path.Combine(cacheDir, "Images", $"{appId}_header.jpg");
                    var coverPath = Path.Combine(cacheDir, "Images", $"{appId}_cover.jpg");

                    if (File.Exists(jsonPath) && File.Exists(headerPath) && File.Exists(coverPath)) continue;

                    await metadataService.FetchMetadataAsync(appId, game);
                    await Task.Delay(1500, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        public List<GameMetadata> GetPlatformGames(string platformId)
        {
            EnsureLoaded();

            lock (_gamesLock) return [.. _allGames.Values.Where(g => g.PlatformId == platformId)];
        }

        public List<GameMetadata> GetAllGames()
        {
            EnsureLoaded();

            lock (_gamesLock) return [.. _allGames.Values];
        }

        public List<GameMetadata> GetFavoriteGames()
        {
            EnsureLoaded();

            lock (_gamesLock) return [.. _allGames.Values.Where(g => g.IsFavorite)];
        }

        public List<GameMetadata> GetHistoryGames()
        {
            EnsureLoaded();

            var settings = SettingsService.LoadSettings();
            var validPlatformPaths = GetValidPlatformPaths(settings);

            var historyList = PlayTimeHistoryFactory.Instance.GetAllHistorySync(validPlatformPaths);

            if (historyList == null || historyList.Count == 0) return [];

            var converter = PathConverterFactory.Create?.Invoke();

            Dictionary<string, GameMetadata> gamesByNormalizedPath;

            lock (_gamesLock)
            {
                gamesByNormalizedPath = _allGames.Values
                    .GroupBy(g =>
                    {
                        var fullPath = g.GetRomFullPath();
                        var friendlyPath = converter?.RealPathToFriendlyPath(fullPath) ?? fullPath;
                        return friendlyPath.Replace("\\", "/").ToLowerInvariant();
                    })
                    .ToDictionary(
                        g => g.Key,
                        g => g.First()
                    );
            }

            var historyWithTime = historyList
                .Select(h => new
                {
                    NormalizedId = h.Id.Replace("\\", "/").ToLowerInvariant(),
                    LastPlayedTime = h.LastPlayedTime ?? DateTime.MinValue
                })
                .ToList();

            return [.. historyWithTime
                .Where(h => gamesByNormalizedPath.ContainsKey(h.NormalizedId))
                .OrderByDescending(h => h.LastPlayedTime)
                .Take(HistoryMaxCount)
                .Select(h => gamesByNormalizedPath[h.NormalizedId])];
        }

        public static int GetHistoryCount()
        {
            var settings = SettingsService.LoadSettings();
            var validPlatformPaths = GetValidPlatformPaths(settings);

            return PlayTimeHistoryFactory.Instance.GetHistoryCountSync(validPlatformPaths);
        }

        private static List<string> GetValidPlatformPaths(AppSettings settings)
        {
            List<string> paths = [];

            if (settings.PlatformSettings == null || settings.RomsBasePaths == null) return paths;

            foreach (var basePath in settings.RomsBasePaths)
            {
                foreach (var value in settings.PlatformSettings.Values)
                {
                    var fullPath = Path.Combine(basePath, value.Name);
                    paths.Add(fullPath);
                }
            }

            return paths;
        }

        public GameMetadata? GetGame(string platformId, string romFile)
        {
            EnsureLoaded();

            lock (_gamesLock) return _allGames.Values.FirstOrDefault(g => g.PlatformId == platformId && g.RomFile == romFile);
        }

        public void AddGame(GameMetadata game)
        {
            EnsureLoaded();

            lock (_gamesLock)
            {
                var key = GetGameKey(game.GetBasePath(), game.RomFile);
                _allGames[key] = game;
            }
        }

        public void UpdateGame(GameMetadata game)
        {
            if (game == null) return;

            EnsureLoaded();

            lock (_gamesLock)
            {
                var key = GetGameKey(game.GetBasePath(), game.RomFile);

                if (_allGames.TryGetValue(key, out var existing)) game.CopyTo(existing);
            }
        }

        public void SavePlatformGames(string platformId)
        {
            EnsureLoaded();

            List<GameMetadata> platformGames;

            lock (_gamesLock) platformGames = [.. _allGames.Values.Where(g => g.PlatformId == platformId)];

            if (platformId == GameMetadataManager.SteamKey || platformId == GameMetadataManager.DesktopKey || platformId == GameMetadataManager.AndroidKey)
            {
                var systemAppsPath = AppSettings.SystemAppsPath;
                MetadataService.SaveMetadata(platformId, systemAppsPath, platformGames);
            }
            else
            {
                var gamesByBasePath = platformGames.GroupBy(g => g.GetBasePath());

                foreach (var group in gamesByBasePath)
                {
                    var basePath = group.Key;
                    MetadataService.SaveMetadata(basePath, [.. group]);
                }
            }
        }

        public void SaveAllGames()
        {
            EnsureLoaded();

            Dictionary<string, List<GameMetadata>> groupedGames;

            lock (_gamesLock)
                groupedGames = _allGames.Values
                    .GroupBy(g => g.PlatformId!)
                    .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var kvp in groupedGames)
                if (!string.IsNullOrEmpty(kvp.Key)) MetadataService.SaveMetadata(kvp.Key, kvp.Value);
        }

        public void ReloadPlatform(string platformId)
        {
            lock (_gamesLock)
            {
                var gamesToRemove = _allGames.Values
                    .Where(g => g.PlatformId == platformId)
                    .ToList();

                foreach (var game in gamesToRemove)
                {
                    var key = GetGameKey(game.GetBasePath(), game.RomFile);
                    _allGames.Remove(key);
                    game.Dispose();
                }

                if (platformId == GameMetadataManager.SteamKey)
                    LoadSteamGames(AppSettings.SystemAppsPath);
                else
                {
                    var settings = SettingsService.LoadSettings();
                    var converter = PathConverterFactory.Create?.Invoke();

                    foreach (var platform in settings.PlatformSettings)
                    {
                        var compositeKey = platform.Key;
                        var realPath = converter?.FriendlyPathToRealPath(compositeKey) ?? compositeKey;
                        var actualPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? PlatformInfoService.NormalizePlatformId(platform.Value.Name);

                        if (actualPlatformId == platformId)
                        {
                            MetadataService.ScanRomsFolder(realPath);
                            var games = MetadataService.LoadMetadata(realPath);

                            foreach (var game in games)
                            {
                                game.PlatformId = actualPlatformId;
                                game.SetBasePath(realPath);
                                var key = GetGameKey(compositeKey, game.RomFile);
                                _allGames[key] = game;
                            }
                        }
                    }
                }
            }
        }

        public void InvalidatePlatformCache()
        {
            lock (_gamesLock)
            {
                _invalidatedPlatforms.Clear();

                _invalidatedPlatforms.Add(GameMetadataManager.AllGamesKey);
                _invalidatedPlatforms.Add(GameMetadataManager.FavoritesKey);
                _invalidatedPlatforms.Add(GameMetadataManager.HistoriesKey);
            }
        }

        public bool IsPlatformInvalidated(string platformId)
        {
            lock (_gamesLock) return _invalidatedPlatforms.Contains(platformId);
        }

        public void ClearPlatformInvalidation(string platformId)
        {
            lock (_gamesLock) _invalidatedPlatforms.Remove(platformId);
        }

        public void Clear()
        {
            _fetchCancellationTokenSource?.Cancel();
            _fetchCancellationTokenSource?.Dispose();
            _fetchCancellationTokenSource = null;

            lock (_gamesLock)
            {
                foreach (var game in _allGames.Values) game.Dispose();

                _allGames.Clear();
                _invalidatedPlatforms.Clear();
                _isLoaded = false;

                GameMetadata.ClearDirectoryCache();
            }
        }

        private static string GetGameKey(string platformId, string romFile) => $"{platformId}|{romFile}";

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _fetchCancellationTokenSource?.Cancel();
            _fetchCancellationTokenSource?.Dispose();

            Clear();
        }
    }
}