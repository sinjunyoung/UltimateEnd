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
        private static readonly Lock _lock = new();
        private readonly Dictionary<string, GameMetadata> _allGames = [];
        private readonly Lock _gamesLock = new();
        private readonly HashSet<string> _loadedPlatforms = [];
        private volatile bool _isFullLoading = false;
        private CancellationTokenSource? _fullLoadCts;
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

        public bool IsLoaded
        {
            get
            {
                lock (_gamesLock) return _loadedPlatforms.Count > 0;
            }
        }

        public bool IsPlatformLoaded(string platformId)
        {
            if (IsSystemAppPlatform(platformId))
            {
                lock (_gamesLock)
                    return _loadedPlatforms.Contains(platformId);
            }

            lock (_gamesLock)
            {
                if (_loadedPlatforms.Contains(platformId)) return true;

                var settings = SettingsService.LoadSettings();
                if (settings.PlatformSettings == null) return false;

                var converter = PathConverterFactory.Create?.Invoke();

                foreach (var platform in settings.PlatformSettings)
                {
                    var compositeKey = platform.Key;
                    var realPath = converter?.FriendlyPathToRealPath(compositeKey) ?? compositeKey;
                    var actualPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? PlatformInfoService.Instance.NormalizePlatformId(platform.Value.Name);

                    if (actualPlatformId == platformId && _loadedPlatforms.Contains(compositeKey)) return true;
                }

                return false;
            }
        }

        private static bool IsSystemAppPlatform(string platformId) =>
            platformId == GameMetadataManager.AndroidKey ||
            platformId == GameMetadataManager.DesktopKey ||
            platformId == GameMetadataManager.SteamKey;

        private AllGamesManager() { }

        public void StartFullLoad()
        {
            if (_isFullLoading) return;

            _fullLoadCts?.Cancel();
            _fullLoadCts = new CancellationTokenSource();
            _isFullLoading = true;

            _ = Task.Run(() => FullLoadWorker(_fullLoadCts.Token));
        }

        public void ResumeFullLoad()
        {
            if (_isFullLoading) return;

            _fullLoadCts?.Cancel();
            _fullLoadCts = new CancellationTokenSource();
            _isFullLoading = true;

            _ = Task.Run(() => FullLoadWorker(_fullLoadCts.Token));
        }

        private void FullLoadWorker(CancellationToken cancellationToken)
        {
            try
            {
                var settings = SettingsService.LoadSettings();

                if (settings.PlatformSettings == null) return;

                var converter = PathConverterFactory.Create?.Invoke();

                foreach (var platform in settings.PlatformSettings)
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    var compositeKey = platform.Key;
                    var realPath = converter?.FriendlyPathToRealPath(compositeKey) ?? compositeKey;
                    var actualPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? PlatformInfoService.Instance.NormalizePlatformId(platform.Value.Name);

                    lock (_gamesLock)
                    {
                        if (_loadedPlatforms.Contains(compositeKey)) continue;
                    }

                    LoadSinglePlatformInternal(compositeKey, realPath, actualPlatformId);

                    lock (_gamesLock) _loadedPlatforms.Add(compositeKey);
                }

                LoadSystemApps(cancellationToken);
            }
            finally
            {
                _isFullLoading = false;
            }
        }

        private void LoadSystemApps(CancellationToken cancellationToken)
        {
            var systemAppsPath = AppSettings.SystemAppsPath;

            if (string.IsNullOrEmpty(systemAppsPath)) return;

            if (cancellationToken.IsCancellationRequested) return;

            if (OperatingSystem.IsWindows())
            {
                lock (_gamesLock)
                {
                    if (!_loadedPlatforms.Contains(GameMetadataManager.SteamKey))
                    {
                        LoadSteamGames(systemAppsPath);
                        _loadedPlatforms.Add(GameMetadataManager.SteamKey);
                    }
                }
            }

            if (cancellationToken.IsCancellationRequested) return;

            var appProvider = AppProviderFactory.Create?.Invoke();
            if (appProvider != null)
            {
                lock (_gamesLock)
                {
                    if (!_loadedPlatforms.Contains(appProvider.PlatformId))
                    {
                        LoadNativeApps(systemAppsPath, appProvider.PlatformId);
                        _loadedPlatforms.Add(appProvider.PlatformId);
                    }
                }
            }
        }

        public void EnsurePlatformLoaded(string platformId)
        {
            if (IsPlatformLoaded(platformId)) return;

            if (_isFullLoading)
            {
                _fullLoadCts?.Cancel();

                var wait = 0;

                while (_isFullLoading && wait < 300)
                {
                    Thread.Sleep(10);
                    wait++;
                }
            }

            if (IsSystemAppPlatform(platformId))
            {
                LoadSystemAppPlatform(platformId);
                ResumeFullLoad();
                return;
            }

            LoadRegularPlatform(platformId);
            ResumeFullLoad();
        }

        private void LoadSystemAppPlatform(string platformId)
        {
            var systemAppsPath = AppSettings.SystemAppsPath;
            if (string.IsNullOrEmpty(systemAppsPath)) return;

            lock (_gamesLock)
            {
                if (_loadedPlatforms.Contains(platformId)) return;

                if (platformId == GameMetadataManager.SteamKey)
                {
                    if (OperatingSystem.IsWindows())
                    {
                        LoadSteamGames(systemAppsPath);
                        _loadedPlatforms.Add(platformId);
                    }
                }
                else if (platformId == GameMetadataManager.AndroidKey || platformId == GameMetadataManager.DesktopKey)
                {
                    LoadNativeApps(systemAppsPath, platformId);
                    _loadedPlatforms.Add(platformId);
                }
            }
        }

        private void LoadRegularPlatform(string platformId)
        {
            var settings = SettingsService.LoadSettings();

            if (settings.PlatformSettings == null) return;

            var converter = PathConverterFactory.Create?.Invoke();

            foreach (var platform in settings.PlatformSettings)
            {
                var compositeKey = platform.Key;
                var realPath = converter?.FriendlyPathToRealPath(compositeKey) ?? compositeKey;
                var actualPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? PlatformInfoService.Instance.NormalizePlatformId(platform.Value.Name);

                if (actualPlatformId == platformId)
                {
                    lock (_gamesLock)
                    {
                        if (_loadedPlatforms.Contains(compositeKey)) continue;
                    }

                    LoadSinglePlatformInternal(compositeKey, realPath, actualPlatformId);

                    lock (_gamesLock) _loadedPlatforms.Add(compositeKey);
                }
            }
        }

        private void LoadSinglePlatformInternal(string compositeKey, string realPath, string actualPlatformId)
        {
            var metadataPath = Path.Combine(realPath, MetadataService.MetadataFileName);

            if (ShouldScan(realPath, metadataPath)) MetadataService.ScanRomsFolder(realPath);

            LoadGamesFromFolder(realPath, actualPlatformId, compositeKey, null);

            if (Directory.Exists(realPath))
            {
                var validExtensions = PlatformInfoService.Instance.GetValidExtensions(actualPlatformId);

                foreach (var subDir in Directory.GetDirectories(realPath))
                {
                    var subFolderName = Path.GetFileName(subDir);

                    if (subFolderName.StartsWith("bios", StringComparison.OrdinalIgnoreCase)) continue;

                    if (validExtensions.Any(ext => File.Exists(Path.Combine(realPath, subFolderName + ext)))) continue;

                    MetadataService.ScanRomsFolder(subDir, actualPlatformId);
                    LoadGamesFromFolder(subDir, actualPlatformId, compositeKey, subFolderName);
                }
            }
        }

        private static bool ShouldScan(string folderPath, string metadataPath)
        {
            if (!File.Exists(metadataPath)) return true;

            try
            {
                var metadataTime = File.GetLastWriteTimeUtc(metadataPath);
                var folderTime = new DirectoryInfo(folderPath).LastWriteTimeUtc;

                return folderTime > metadataTime;
            }
            catch
            {
                return true;
            }
        }

        private void LoadGamesFromFolder(string folderPath, string platformId, string compositeKey, string? subFolder)
        {
            var games = MetadataService.LoadMetadataFromRealPath(platformId, folderPath);

            foreach (var game in games)
            {
                game.PlatformId = platformId;
                game.SubFolder = subFolder;
                game.SetBasePath(folderPath);

                var key = GetGameKey(compositeKey, subFolder, game.RomFile);

                lock (_gamesLock)
                {
                    if (_allGames.ContainsKey(key)) continue;
                    _allGames[key] = game;
                }
            }
        }

        private void LoadNativeApps(string systemAppsPath, string platformId)
        {
            try
            {
                var games = MetadataService.LoadMetadata(platformId, systemAppsPath);
                var realSystemAppsPath = systemAppsPath;
                var converter = PathConverterFactory.Create?.Invoke();

                if (converter != null) realSystemAppsPath = converter.FriendlyPathToRealPath(systemAppsPath) ?? systemAppsPath;

                foreach (var game in games)
                {
                    game.PlatformId = platformId;
                    game.SetBasePath(Path.Combine(realSystemAppsPath, platformId));
                    var key = GetGameKey(platformId, game.SubFolder, game.RomFile);

                    lock (_gamesLock)
                    {
                        if (_allGames.ContainsKey(key)) continue;

                        _allGames[key] = game;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error loading native apps for platform {platformId}: {e}");
            }
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
                    var key = GetGameKey(GameMetadataManager.SteamKey, game.SubFolder, game.RomFile);

                    lock (_gamesLock)
                    {
                        if (_allGames.ContainsKey(key)) continue;
                    }

                    if (metadataDict != null && metadataDict.TryGetValue(game.RomFile, out var savedGame))
                    {
                        lock (_gamesLock) _allGames[key] = savedGame;
                    }
                    else
                    {
                        SteamMetadataService.TryLoadFromCache(appId, game);
                        lock (_gamesLock) _allGames[key] = game;
                        gamesToFetch.Add((appId, game));
                    }
                }

                var steamGamesList = new List<GameMetadata>();

                lock (_gamesLock) steamGamesList = [.. _allGames.Values.Where(g => g.PlatformId == GameMetadataManager.SteamKey)];

                MetadataService.SaveMetadata(GameMetadataManager.SteamKey, systemAppsPath, steamGamesList);

                if (gamesToFetch.Count > 0) _ = FetchSteamMetadataInBackgroundAsync(gamesToFetch, _fetchCancellationTokenSource.Token);
            }
            catch { }
        }

        private static async Task FetchSteamMetadataInBackgroundAsync(List<(string appId, GameMetadata game)> gamesToFetch, CancellationToken cancellationToken)
        {
            foreach (var (appId, game) in gamesToFetch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var basePath = game.GetBasePath();
                    var logoPath = Path.Combine(basePath, "logos", $"{appId}.jpg");
                    var coverPath = Path.Combine(basePath, "covers", $"{appId}.jpg");

                    var cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltimateEnd", "Cache", "Steam");
                    var jsonPath = Path.Combine(cacheDir, $"{appId}.json");

                    if (File.Exists(jsonPath) && File.Exists(logoPath) && File.Exists(coverPath)) continue;

                    await SteamMetadataService.FetchMetadataAsync(appId, game);
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
            if (platformId == GameMetadataManager.SteamKey && !IsPlatformLoaded(platformId))
            {
                var systemAppsPath = AppSettings.SystemAppsPath;

                if (!string.IsNullOrEmpty(systemAppsPath) && OperatingSystem.IsWindows())
                {
                    lock (_gamesLock)
                    {
                        if (!_loadedPlatforms.Contains(GameMetadataManager.SteamKey))
                        {
                            LoadSteamGames(systemAppsPath);
                            _loadedPlatforms.Add(GameMetadataManager.SteamKey);
                        }
                    }
                }
            }

            lock (_gamesLock) return [.. _allGames.Values.Where(g => g.PlatformId == platformId)];
        }

        public List<GameMetadata> GetAllGames()
        {
            lock (_gamesLock) return [.. _allGames.Values];
        }

        public List<GameMetadata> GetFavoriteGames()
        {
            lock (_gamesLock) return [.. _allGames.Values.Where(g => g.IsFavorite)];
        }

        public List<GameMetadata> GetHistoryGames()
        {
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
            lock (_gamesLock) return _allGames.Values.FirstOrDefault(g => g.PlatformId == platformId && g.RomFile == romFile);
        }

        public void AddGame(GameMetadata game)
        {
            lock (_gamesLock)
            {
                var key = GetGameKey(game.GetBasePath(), game.SubFolder, game.RomFile);
                _allGames[key] = game;
            }
        }

        public void UpdateGame(GameMetadata game)
        {
            if (game == null) return;

            GameMetadata? existing;

            lock (_gamesLock)
            {
                var key = GetGameKey(game.GetBasePath(), game.SubFolder, game.RomFile);
                _allGames.TryGetValue(key, out existing);
            }

            if (existing != null) game.CopyTo(existing);
        }

        public void UpdateGameKey(GameMetadata game, string oldSubFolder, string newSubFolder)
        {
            lock (_gamesLock)
            {
                var oldKey = GetGameKey(game.GetBasePath(), oldSubFolder, game.RomFile);
                var newKey = GetGameKey(game.GetBasePath(), newSubFolder, game.RomFile);

                if (_allGames.TryGetValue(oldKey, out var existing))
                {
                    _allGames.Remove(oldKey);
                    _allGames[newKey] = existing;
                }
            }
        }

        public void SavePlatformGames(string platformId)
        {
            List<GameMetadata> platformGames;

            lock (_gamesLock) platformGames = [.. _allGames.Values.Where(g => g.PlatformId == platformId)];

            if (IsSystemAppPlatform(platformId))
            {
                var systemAppsPath = AppSettings.SystemAppsPath;
                MetadataService.SaveMetadata(platformId, systemAppsPath, platformGames);
            }
            else
            {
                var gamesByBasePath = platformGames.GroupBy(g => g.GetBasePath());

                foreach (var group in gamesByBasePath)
                {
                    var realPath = group.Key;
                    MetadataService.SaveMetadataToRealPath(realPath, [.. group]);
                }
            }
        }

        public void SaveAllGames()
        {
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
                    var key = GetGameKey(game.GetBasePath(), game.SubFolder, game.RomFile);
                    _allGames.Remove(key);
                    game.Dispose();
                }

                var settings = SettingsService.LoadSettings();

                if (settings.PlatformSettings != null)
                {
                    var converter = PathConverterFactory.Create?.Invoke();

                    foreach (var platform in settings.PlatformSettings)
                    {
                        var compositeKey = platform.Key;
                        var realPath = converter?.FriendlyPathToRealPath(compositeKey) ?? compositeKey;
                        var actualPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? PlatformInfoService.Instance.NormalizePlatformId(platform.Value.Name);

                        if (actualPlatformId == platformId) _loadedPlatforms.Remove(compositeKey);
                    }
                }
            }

            if (IsSystemAppPlatform(platformId))
                LoadSystemAppPlatform(platformId);
            else
                LoadRegularPlatform(platformId);
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
            _fullLoadCts?.Cancel();
            _fetchCancellationTokenSource?.Cancel();
            _fetchCancellationTokenSource?.Dispose();
            _fetchCancellationTokenSource = null;

            lock (_gamesLock)
            {
                foreach (var game in _allGames.Values) game.Dispose();

                _allGames.Clear();
                _invalidatedPlatforms.Clear();
                _loadedPlatforms.Clear();

                GameMetadata.ClearDirectoryCache();
            }

            _isFullLoading = false;
        }

        private static string GetGameKey(string platformId, string? subFolder, string romFile)
        {
            var subFolderPart = string.IsNullOrEmpty(subFolder) ? string.Empty : subFolder;

            return $"{platformId}|{subFolderPart}|{romFile}";
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _fullLoadCts?.Cancel();
            _fetchCancellationTokenSource?.Cancel();
            _fetchCancellationTokenSource?.Dispose();

            Clear();
        }
    }
}