using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Managers;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class MetadataService
    {
        public const string MetadataFileName = "metadata.txt";
        private const string PegasusMetadataFileName = "metadata.pegasus.txt";

        private static readonly ConcurrentDictionary<string, (bool hasGames, DateTime scanned)> _hasGamesCache = new();
        private static readonly TimeSpan CacheValidDuration = TimeSpan.FromMinutes(30);

        public static void PreloadHasGamesCache(IEnumerable<string> platformKeys)
        {
            _ = Task.Run(() =>
            {
                Parallel.ForEach(platformKeys,
                    new ParallelOptions { MaxDegreeOfParallelism = 3 },
                    platformKey =>
                    {
                        try
                        {
                            HasGames(platformKey);
                        }
                        catch { }
                    });
            });
        }

        public static void ClearCache() => _hasGamesCache.Clear();

        public static void InvalidatePlatformCache(string platformKey) => _hasGamesCache.TryRemove(platformKey, out _);

        public static List<GameMetadata> LoadMetadata(string compositeKey)
        {
            var realPath = SettingsService.GetPlatformPath(compositeKey);
            string mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(compositeKey) ?? compositeKey;

            return LoadMetadataFromRealPath(mappedPlatformId, realPath);
        }

        public static List<GameMetadata> LoadMetadata(string platformId, string basePath)
        {
            var realPath = Path.Combine(basePath, platformId);
            string mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(platformId) ?? platformId;

            return LoadMetadataFromRealPath(mappedPlatformId, realPath);
        }

        public static List<GameMetadata> LoadMetadataFromRealPath(string platformId, string realPath)
        {
            if (string.IsNullOrEmpty(realPath) || !Directory.Exists(realPath)) return [];

            var validExtensions = PlatformInfoService.Instance.GetValidExtensions(platformId);

            string metadataFilePath = Path.Combine(realPath, MetadataFileName);

            if (File.Exists(metadataFilePath))
            {
                try
                {
                    var games = GameMetadataParser.Parse(metadataFilePath, realPath);

                    return [.. games.Where(g =>
                    {
                        if (string.IsNullOrEmpty(g.RomFile)) return false;
                        if (IsSystemApp(platformId)) return true;

                        var ext = Path.GetExtension(g.RomFile);

                        return validExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                    })];
                }
                catch (Exception)
                {
                    throw;
                }
            }

            metadataFilePath = Path.Combine(realPath, PegasusMetadataFileName);

            if (File.Exists(metadataFilePath))
            {
                try
                {
                    var games = PegasusMetadataParser.Parse(metadataFilePath);

                    if (games.Count > 0)
                    {
                        games = [.. games.Where(g =>
                        {
                            if (string.IsNullOrEmpty(g.RomFile)) return false;
                            if (IsSystemApp(platformId)) return true;

                            var ext = Path.GetExtension(g.RomFile);

                            return validExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                        })];
                    }

                    return games;
                }
                catch { }
            }

            return [];
        }

        public static void SaveMetadata(string compositeKey, IEnumerable<GameMetadata> games)
        {
            var realPath = SettingsService.GetPlatformPath(compositeKey);
            SaveMetadataToRealPath(realPath, games);
            InvalidatePlatformCache(compositeKey);
        }

        public static void SaveMetadata(string platformId, string basePath, IEnumerable<GameMetadata> games)
        {
            var realPath = Path.Combine(basePath, platformId);
            SaveMetadataToRealPath(realPath, games);
            InvalidatePlatformCache(platformId);
        }

        public static void SaveMetadataToRealPath(string realPath, IEnumerable<GameMetadata> games)
        {
            if (string.IsNullOrEmpty(realPath)) return;

            var converter = PathConverterFactory.Create?.Invoke();
            var actualPath = converter?.FriendlyPathToRealPath(realPath) ?? realPath;

            if (!Directory.Exists(actualPath)) Directory.CreateDirectory(actualPath);

            var filePath = Path.Combine(actualPath, MetadataFileName);

            try
            {
                GameMetadataParser.Write(filePath, games);
            }
            catch (Exception)
            {
                throw;
            }

            var settings = SettingsService.LoadSettings();
            if (settings.PlatformSettings != null)
            {
                foreach (var platform in settings.PlatformSettings)
                {
                    var compositeKey = platform.Key;
                    var keyRealPath = converter?.FriendlyPathToRealPath(compositeKey) ?? compositeKey;

                    if (keyRealPath.Equals(actualPath, StringComparison.OrdinalIgnoreCase))
                    {
                        InvalidatePlatformCache(compositeKey);
                        break;
                    }
                }
            }
        }

        public static void ScanRomsFolder(string compositeKey)
        {
            try
            {
                var realPath = SettingsService.GetPlatformPath(compositeKey);

                if (string.IsNullOrEmpty(realPath) || !Directory.Exists(realPath)) return;

                string mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(compositeKey) ?? compositeKey;

                ScanRomsFolderInternal(compositeKey, realPath, mappedPlatformId);
            }
            catch { }
        }

        public static void ScanRomsFolder(string realPath, string platformId)
        {
            try
            {
                if (string.IsNullOrEmpty(realPath) || !Directory.Exists(realPath)) return;

                var converter = PathConverterFactory.Create?.Invoke();
                var friendlyPath = converter?.RealPathToFriendlyPath(realPath) ?? realPath;

                ScanRomsFolderInternal(friendlyPath, realPath, platformId);
            }
            catch { }
        }

        private static void ScanRomsFolderInternal(string compositeKey, string realPath, string platformId)
        {
            var validExtensions = PlatformInfoService.Instance.GetValidExtensions(platformId);

            List<GameMetadata> existingMetadata;

            try
            {
                existingMetadata = LoadMetadataFromRealPath(platformId, realPath);
            }
            catch (Exception)
            {
                return;
            }

            var existingRomFiles = new HashSet<string>(
                existingMetadata.Select(g => $"{g.SubFolder ?? string.Empty}|{g.RomFile}"),
                StringComparer.OrdinalIgnoreCase);
            var addedCount = 0;

            ScanFolder(realPath, null, validExtensions, existingRomFiles, existingMetadata, ref addedCount);

            try
            {
                var topLevelRomNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var ext in validExtensions)
                {
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(realPath, $"*{ext}", SearchOption.TopDirectoryOnly))
                            topLevelRomNames.Add(Path.GetFileNameWithoutExtension(file));
                    }
                    catch { }
                }

                foreach (var subDir in Directory.EnumerateDirectories(realPath))
                {
                    var subFolderName = Path.GetFileName(subDir);

                    if (IsBiosFolder(subFolderName)) continue;

                    if (topLevelRomNames.Contains(subFolderName)) continue;

                    ScanFolder(subDir, subFolderName, validExtensions, existingRomFiles, existingMetadata, ref addedCount);
                }
            }
            catch { }

            if (addedCount > 0)
            {
                try
                {
                    SaveMetadata(compositeKey, existingMetadata);
                }
                catch { }
            }
        }

        private static void ScanFolder(string realFolderPath, string? subFolder, IEnumerable<string> validExtensions, HashSet<string> existingRomFiles, List<GameMetadata> existingMetadata, ref int addedCount)
        {
            string[] romFiles;

            try
            {
                romFiles = Directory.GetFiles(realFolderPath, "*.*", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return;
            }

            foreach (var romFile in romFiles)
            {
                var fileName = Path.GetFileName(romFile);
                var ext = Path.GetExtension(fileName);

                if (!validExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                var key = $"{subFolder ?? string.Empty}|{fileName}";

                if (!existingRomFiles.Contains(key))
                {
                    var newGame = new GameMetadata
                    {
                        RomFile = fileName,
                        SubFolder = subFolder
                    };
                    newGame.SetBasePath(realFolderPath);
                    existingMetadata.Add(newGame);
                    existingRomFiles.Add(key);
                    addedCount++;
                }
            }
        }

        private static HashSet<GameMetadata> UpdateFromExternalMetadata(ObservableCollection<GameMetadata> games, string platformId, string metadataFileName, Func<string, List<GameMetadata>> parser, CancellationToken cancellationToken)
        {
            var mappingConfig = PlatformMappingService.Instance.LoadMapping();

            cancellationToken.ThrowIfCancellationRequested();

            var mappedCompositeKeys = mappingConfig.FolderMappings
                .Where(kvp => kvp.Value.Equals(platformId, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();

            if (mappedCompositeKeys.Count == 0) mappedCompositeKeys.Add(platformId);

            var allActualRomFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validRealPaths = new Dictionary<string, string>();

            foreach (var compositeKey in mappedCompositeKeys)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var realPath = SettingsService.GetPlatformPath(compositeKey);

                if (!string.IsNullOrEmpty(realPath) && Directory.Exists(realPath))
                {
                    var romFiles = GetActualRomFiles(realPath, compositeKey);

                    foreach (var romFile in romFiles)
                    {
                        if (!validRealPaths.ContainsKey(romFile))
                        {
                            validRealPaths[romFile] = realPath;
                            allActualRomFiles.Add(romFile);
                        }
                    }
                }
            }

            if (validRealPaths.Count == 0) return [];

            if (!File.Exists(metadataFileName)) return [];

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var existingGameDict = games
                    .Where(g => !string.IsNullOrEmpty(g.RomFile))
                    .GroupBy(g => g.RomFile, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.First(),
                        StringComparer.OrdinalIgnoreCase
                    );

                var externalGames = parser(metadataFileName);
                var changedGames = new HashSet<GameMetadata>();

                foreach (var externalGame in externalGames)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrEmpty(externalGame.RomFile)) continue;

                    if (!allActualRomFiles.Contains(externalGame.RomFile)) continue;

                    if (existingGameDict.TryGetValue(externalGame.RomFile, out var existingGame))
                    {
                        if (existingGame.MergeFrom(externalGame))
                            changedGames.Add(existingGame);
                    }
                    else
                    {
                        if (validRealPaths.TryGetValue(externalGame.RomFile, out var actualRealPath))
                        {
                            var compositeKey = mappedCompositeKeys.FirstOrDefault(key => SettingsService.GetPlatformPath(key) == actualRealPath);

                            if (!string.IsNullOrEmpty(compositeKey)) externalGame.PlatformId = compositeKey;

                            externalGame.SetBasePath(actualRealPath);
                            games.Add(externalGame);
                            changedGames.Add(externalGame);
                        }
                    }
                }

                if (changedGames.Count > 0)
                {
                    var gamesByCompositeKey = games
                        .Where(g => mappedCompositeKeys.Contains(g.PlatformId))
                        .GroupBy(g => g.PlatformId);

                    foreach (var group in gamesByCompositeKey)
                        if (!string.IsNullOrEmpty(group.Key)) SaveMetadata(group.Key, [.. group]);
                }

                return changedGames;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static HashSet<GameMetadata> UpdateFromPegasusMetadata(ObservableCollection<GameMetadata> games, string platformId, string pegasusFileName, CancellationToken cancellationToken = default) => UpdateFromExternalMetadata(games, platformId, pegasusFileName, PegasusMetadataParser.Parse, cancellationToken);

        public static HashSet<GameMetadata> UpdateFromEsDeMetadata(ObservableCollection<GameMetadata> games, string platformId, string esdeFileName, CancellationToken cancellationToken = default) => UpdateFromExternalMetadata(games, platformId, esdeFileName, EsDeMetadataParser.Parse, cancellationToken);

        private static HashSet<string> GetActualRomFiles(string realPath, string compositeKey)
        {
            var actualRomFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(realPath)) return actualRomFiles;

            var actualPath = GetRealPath(realPath);
            if (!Directory.Exists(actualPath)) return actualRomFiles;

            string mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(compositeKey) ?? compositeKey;
            var validExtensions = PlatformInfoService.Instance.GetValidExtensions(mappedPlatformId);

            CollectRomFilesFromDirectory(actualPath, validExtensions, actualRomFiles);

            return actualRomFiles;
        }

        private static void CollectRomFilesFromDirectory(string realDirPath, IEnumerable<string> validExtensions, HashSet<string> romFiles)
        {
            ScanFolderForFiles(realDirPath, validExtensions, romFiles);

            var topLevelRomNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ext in validExtensions)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(realDirPath, $"*{ext}", SearchOption.TopDirectoryOnly))
                        topLevelRomNames.Add(Path.GetFileNameWithoutExtension(file));
                }
                catch { }
            }

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(realDirPath))
                {
                    var subFolderName = Path.GetFileName(subDir);

                    if (IsBiosFolder(subFolderName)) continue;

                    if (topLevelRomNames.Contains(subFolderName)) continue;

                    ScanFolderForFiles(subDir, validExtensions, romFiles);
                }
            }
            catch { }
        }

        private static void ScanFolderForFiles(string realFolderPath, IEnumerable<string> validExtensions, HashSet<string> actualRomFiles)
        {
            foreach (var ext in validExtensions)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(realFolderPath, $"*{ext}", SearchOption.TopDirectoryOnly))
                        actualRomFiles.Add(Path.GetFileName(file));
                }
                catch { }
            }
        }

        public static bool HasGames(string compositeKey)
        {
            if (_hasGamesCache.TryGetValue(compositeKey, out var cached))
            {
                if (DateTime.UtcNow - cached.scanned < CacheValidDuration) return cached.hasGames;
            }

            bool result = ScanForGamesOptimized(compositeKey);
            _hasGamesCache[compositeKey] = (result, DateTime.UtcNow);

            return result;
        }

        private static bool ScanForGamesOptimized(string compositeKey)
        {
            var realPath = SettingsService.GetPlatformPath(compositeKey);

            if (string.IsNullOrEmpty(realPath)) return false;

            var actualPath = GetRealPath(realPath);

            if (!Directory.Exists(actualPath)) return false;

            string mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(compositeKey) ?? compositeKey;
            var validExtensions = PlatformInfoService.Instance.GetValidExtensions(mappedPlatformId);

            var extensionSet = new HashSet<string>(validExtensions, StringComparer.OrdinalIgnoreCase);

            try
            {
                if (HasAnyValidFile(actualPath, extensionSet)) return true;

                foreach (var subDir in Directory.EnumerateDirectories(actualPath))
                {
                    var subFolderName = Path.GetFileName(subDir);

                    if (!string.IsNullOrEmpty(subFolderName) && subFolderName.StartsWith("bios", StringComparison.OrdinalIgnoreCase)) continue;

                    if (HasAnyValidFile(subDir, extensionSet)) return true;
                }
            }
            catch
            {
                return false;
            }

            var metadataPath = Path.Combine(actualPath, MetadataFileName);

            if (File.Exists(metadataPath) && new FileInfo(metadataPath).Length > 10) return true;

            var pegasusPath = Path.Combine(actualPath, PegasusMetadataFileName);

            if (File.Exists(pegasusPath) && new FileInfo(pegasusPath).Length > 10) return true;

            return false;
        }

        private static bool HasAnyValidFile(string realFolderPath, HashSet<string> validExtensions)
        {
            try
            {
                return Directory.EnumerateFiles(realFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Any(file =>
                    {
                        var ext = Path.GetExtension(file);

                        return !string.IsNullOrEmpty(ext) && validExtensions.Contains(ext);
                    });
            }
            catch
            {
                return false;
            }
        }

        private static string GetRealPath(string path)
        {
            var converter = PathConverterFactory.Create?.Invoke();

            return converter?.FriendlyPathToRealPath(path) ?? path;
        }

        private static bool IsBiosFolder(string folderName) => !string.IsNullOrEmpty(folderName) && folderName.StartsWith("bios", StringComparison.OrdinalIgnoreCase);

        private static bool IsSystemApp(string platformId) => platformId == GameMetadataManager.SteamKey || platformId == GameMetadataManager.DesktopKey || platformId == GameMetadataManager.AndroidKey;
    }
}