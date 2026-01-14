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
        private const string MetadataFileName = "metadata.txt";
        private const string PegasusMetadataFileName = "metadata.pegasus.txt";

        private static readonly ConcurrentDictionary<string, (bool hasGames, DateTime scanned)> _hasGamesCache = new();
        private static readonly TimeSpan CacheValidDuration = TimeSpan.FromMinutes(30);

        public static async Task PreloadAllPlatformsAsync(IEnumerable<string> platformKeys)
        {
            await Task.Run(() =>
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

        public static void PreloadAllPlatforms(IEnumerable<string> platformKeys)
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

        public static void InvalidatePlatformCache(string platformKey)
            => _hasGamesCache.TryRemove(platformKey, out _);

        public static List<GameMetadata> LoadMetadata(string platformId)
        {
            var platformPath = SettingsService.GetPlatformPath(platformId);

            return LoadMetadataFromPath(platformId, platformPath);
        }

        public static List<GameMetadata> LoadMetadata(string platformId, string basePath)
        {
            var platformPath = Path.Combine(basePath, platformId);

            return LoadMetadataFromPath(platformId, platformPath);
        }

        private static List<GameMetadata> LoadMetadataFromPath(string platformId, string platformPath)
        {
            if (string.IsNullOrEmpty(platformPath) || !Directory.Exists(platformPath)) return [];

            string fileName = Path.Combine(platformPath, MetadataFileName);

            if (File.Exists(fileName))
            {
                try
                {
                    return GameMetadataParser.Parse(fileName, platformPath);
                }
                catch (Exception)
                {
                    throw;
                }
            }

            fileName = Path.Combine(platformPath, PegasusMetadataFileName);

            if (File.Exists(fileName))
            {
                try
                {
                    var games = PegasusMetadataParser.Parse(fileName);

                    if (games.Count > 0)
                    {
                        if (IsSystemApp(platformId))
                        {
                            var settings = SettingsService.LoadSettings();
                            SaveMetadata(platformId, AppSettings.SystemAppsPath, games);
                        }
                        else SaveMetadata(platformId, games);
                    }

                    return games;
                }
                catch { }
            }

            return [];
        }

        public static void SaveMetadata(string platformId, IEnumerable<GameMetadata> games)
        {
            var platformPath = SettingsService.GetPlatformPath(platformId);

            SaveMetadataToPath(platformPath, games);
            InvalidatePlatformCache(platformId);
        }

        public static void SaveMetadata(string platformId, string basePath, IEnumerable<GameMetadata> games)
        {
            var platformPath = Path.Combine(basePath, platformId);

            SaveMetadataToPath(platformPath, games);
            InvalidatePlatformCache(platformId);
        }

        private static void SaveMetadataToPath(string platformPath, IEnumerable<GameMetadata> games)
        {
            if (string.IsNullOrEmpty(platformPath)) return;

            var converter = PathConverterFactory.Create?.Invoke();
            var realPath = converter?.FriendlyPathToRealPath(platformPath) ?? platformPath;

            if (!Directory.Exists(realPath)) Directory.CreateDirectory(realPath);

            var filePath = Path.Combine(realPath, MetadataFileName);

            try
            {
                GameMetadataParser.Write(filePath, games);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static void ScanRomsFolder(string realPath)
        {
            try
            {
                var platformPath = SettingsService.GetPlatformPath(realPath);

                if (string.IsNullOrEmpty(platformPath) || !Directory.Exists(platformPath)) return;

                string mappedId = PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? realPath;
                var validExtensions = PlatformInfoService.Instance.GetValidExtensions(mappedId);

                List<GameMetadata> existingMetadata;

                try
                {
                    existingMetadata = LoadMetadata(realPath);
                }
                catch (Exception)
                {
                    return;
                }

                var existingRomFiles = new HashSet<string>(existingMetadata.Select(g => $"{g.SubFolder ?? ""}|{g.RomFile}"), StringComparer.OrdinalIgnoreCase);
                var addedCount = 0;

                ScanFolder(platformPath, null, validExtensions, existingRomFiles, existingMetadata, ref addedCount);

                try
                {
                    foreach (var subDir in Directory.GetDirectories(platformPath))
                    {
                        var subFolderName = Path.GetFileName(subDir);
                        ScanFolder(subDir, subFolderName, validExtensions, existingRomFiles, existingMetadata, ref addedCount);
                    }
                }
                catch { }

                if (addedCount > 0)
                {
                    try
                    {
                        SaveMetadata(realPath, existingMetadata);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void ScanFolder(string folderPath, string? subFolder, IEnumerable<string> validExtensions, HashSet<string> existingRomFiles, List<GameMetadata> existingMetadata, ref int addedCount)
        {
            string[] romFiles;

            try
            {
                romFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
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

                var key = $"{subFolder ?? ""}|{fileName}";

                if (!existingRomFiles.Contains(key))
                {
                    var newGame = new GameMetadata
                    {
                        RomFile = fileName,
                        SubFolder = subFolder
                    };
                    newGame.SetBasePath(folderPath);
                    existingMetadata.Add(newGame);
                    addedCount++;
                }
            }
        }

        private static HashSet<GameMetadata> UpdateFromExternalMetadata(ObservableCollection<GameMetadata> games, string platformId, string metadataFileName, Func<string, List<GameMetadata>> parser, CancellationToken cancellationToken)
        {
            var mappingConfig = PlatformMappingService.Instance.LoadMapping();

            cancellationToken.ThrowIfCancellationRequested();

            var mappedFolderIds = mappingConfig.FolderMappings
                .Where(kvp => kvp.Value.Equals(platformId, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Key)
                .ToList();

            if (mappedFolderIds.Count == 0) mappedFolderIds.Add(platformId);

            var allActualRomFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validPlatformPaths = new Dictionary<string, string>();

            foreach (var folderId in mappedFolderIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var platformPath = SettingsService.GetPlatformPath(folderId);

                if (!string.IsNullOrEmpty(platformPath) && Directory.Exists(platformPath))
                {
                    var romFiles = GetActualRomFiles(platformPath, folderId);

                    foreach (var romFile in romFiles)
                    {
                        if (!validPlatformPaths.ContainsKey(romFile))
                        {
                            validPlatformPaths[romFile] = platformPath;
                            allActualRomFiles.Add(romFile);
                        }
                    }
                }
            }

            if (validPlatformPaths.Count == 0) return [];

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
                        if (validPlatformPaths.TryGetValue(externalGame.RomFile, out var actualPlatformPath))
                        {
                            var folderId = mappedFolderIds.FirstOrDefault(id =>
                                SettingsService.GetPlatformPath(id) == actualPlatformPath);

                            if (!string.IsNullOrEmpty(folderId)) externalGame.PlatformId = folderId;

                            externalGame.SetBasePath(actualPlatformPath);
                            games.Add(externalGame);
                            changedGames.Add(externalGame);
                        }
                    }
                }

                if (changedGames.Count > 0)
                {
                    var gamesByFolder = games
                        .Where(g => mappedFolderIds.Contains(g.PlatformId))
                        .GroupBy(g => g.PlatformId);

                    foreach (var group in gamesByFolder)
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

        private static HashSet<string> GetActualRomFiles(string platformPath, string platformKey)
        {
            var actualRomFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(platformPath)) return actualRomFiles;

            var converter = PathConverterFactory.Create?.Invoke();
            var realPath = converter?.FriendlyPathToRealPath(platformPath) ?? platformPath;

            if (!Directory.Exists(realPath)) return actualRomFiles;

            string mappedId = PlatformMappingService.Instance.GetMappedPlatformId(platformKey) ?? platformKey;
            var validExtensions = PlatformInfoService.Instance.GetValidExtensions(mappedId);

            ScanFolderForFiles(realPath, validExtensions, actualRomFiles);

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(realPath))
                    ScanFolderForFiles(subDir, validExtensions, actualRomFiles);
            }
            catch { }

            return actualRomFiles;
        }

        private static void ScanFolderForFiles(string folderPath, IEnumerable<string> validExtensions, HashSet<string> actualRomFiles)
        {
            foreach (var ext in validExtensions)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folderPath, $"*{ext}", SearchOption.TopDirectoryOnly))
                        actualRomFiles.Add(Path.GetFileName(file));
                }
                catch { }
            }
        }

        public static bool HasGames(string platformKey)
        {
            if (_hasGamesCache.TryGetValue(platformKey, out var cached))
            {
                if (DateTime.UtcNow - cached.scanned < CacheValidDuration) return cached.hasGames;
            }

            bool result = ScanForGames(platformKey);

            _hasGamesCache[platformKey] = (result, DateTime.UtcNow);

            return result;
        }

        private static bool ScanForGames(string platformKey)
        {
            var platformPath = SettingsService.GetPlatformPath(platformKey);

            if (string.IsNullOrEmpty(platformPath)) return false;

            var converter = PathConverterFactory.Create?.Invoke();
            var realPath = converter?.FriendlyPathToRealPath(platformPath) ?? platformPath;

            if (!Directory.Exists(realPath)) return false;

            string mappedId = PlatformMappingService.Instance.GetMappedPlatformId(platformKey) ?? platformKey;
            var validExtensions = PlatformInfoService.Instance.GetValidExtensions(mappedId);

            try
            {
                if (HasFilesInFolder(realPath, validExtensions)) return true;

                foreach (var subDir in Directory.EnumerateDirectories(realPath))
                {
                    if (HasFilesInFolder(subDir, validExtensions)) return true;
                }
            }
            catch
            {
                return false;
            }

            var metadataPath = Path.Combine(realPath, MetadataFileName);

            if (File.Exists(metadataPath))
            {
                var fileInfo = new FileInfo(metadataPath);

                if (fileInfo.Length > 10)
                {
                    try
                    {
                        var games = LoadMetadata(platformKey);

                        return games.Count > 0;
                    }
                    catch { }
                }
            }

            var pegasusPath = Path.Combine(realPath, PegasusMetadataFileName);

            if (File.Exists(pegasusPath))
            {
                var fileInfo = new FileInfo(pegasusPath);

                if (fileInfo.Length > 10)
                {
                    try
                    {
                        var games = PegasusMetadataParser.Parse(pegasusPath);

                        return games.Count > 0;
                    }
                    catch { }
                }
            }

            return false;
        }

        private static bool HasFilesInFolder(string folderPath, IEnumerable<string> validExtensions)
        {
            foreach (var ext in validExtensions)
            {
                var firstFile = Directory.EnumerateFiles(folderPath, $"*{ext}", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();

                if (firstFile != null) return true;
            }
            return false;
        }

        private static bool IsSystemApp(string platformId) => platformId == GameMetadataManager.SteamKey || platformId == GameMetadataManager.DesktopKey || platformId == GameMetadataManager.AndroidKey;
    }
}