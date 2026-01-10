using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class GameMediaPathResolver
    {
        private static readonly ConcurrentDictionary<string, string[]> _directoryCache = [];

        public static string GetCoverPath(GameMetadata game, string basePath)
        {
            string[] folderCandidates = { "covers", "cover", "boxart", "boxfront", "3dboxes" };
            string[] pegasusFileNames = { "boxFront.png", "boxFront.jpg" };

            return GetMediaPath(game.CoverImagePath, game.RomFile, basePath, "Covers", folderCandidates, ".png", pegasusFileNames);
        }

        public static string GetLogoPath(GameMetadata game, string basePath)
        {
            string[] folderCandidates = { "logos", "logo", "marquee", "marquees", "wheel" };
            string[] pegasusFileNames = { "logo.png", "logo.jpg" };

            return GetMediaPath(game.LogoImagePath, game.RomFile, basePath, "Logos", folderCandidates, ".png", pegasusFileNames);
        }

        public static string GetVideoPath(GameMetadata game, string basePath)
        {
            string[] folderCandidates = { "videos", "video", "snap" };
            string[] pegasusFileNames = { "video.mp4", "video.mkv" };

            return GetMediaPath(game.VideoPath, game.RomFile, basePath, "Videos", folderCandidates, ".mp4", pegasusFileNames);
        }

        private static string GetMediaPath(string? explicitPathProperty, string romFile, string basePath, string defaultBaseFolder, string[] folderCandidates, string defaultExtension, string[] pegasusFileNames)
        {
            if (!string.IsNullOrEmpty(explicitPathProperty))
            {
                string absolutePath;

                if (explicitPathProperty.StartsWith("content://")) return explicitPathProperty;

                if (Path.IsPathRooted(explicitPathProperty))
                    absolutePath = explicitPathProperty;
                else
                    absolutePath = Path.Combine(basePath, explicitPathProperty);

                if (File.Exists(absolutePath)) return absolutePath;
            }

            var fileName = Path.GetFileNameWithoutExtension(romFile);

            var pegasusMediaPath = TryGetPegasusMediaPath(basePath, fileName, pegasusFileNames);

            if (pegasusMediaPath != null) return pegasusMediaPath;

            var actualDirs = _directoryCache.GetOrAdd(basePath, path => Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>());

            foreach (var candidate in folderCandidates)
            {
                var matched = actualDirs.FirstOrDefault(d => string.Equals(Path.GetFileName(d), candidate, StringComparison.OrdinalIgnoreCase));

                if (matched != null)
                {
                    var path = Path.Combine(matched, fileName + defaultExtension);

                    if (File.Exists(path)) return path;
                }
            }

            return Path.Combine(basePath, defaultBaseFolder, fileName + defaultExtension);
        }

        private static string? TryGetPegasusMediaPath(string basePath, string romFileNameWithoutExt, string[] fileNames)
        {
            string[] folderNames = [ "scrap", "media" ];

            foreach (var folderName in folderNames)
            {
                var mediaFolder = Path.Combine(basePath, folderName, romFileNameWithoutExt);

                if (Directory.Exists(mediaFolder))
                {
                    foreach (var fileName in fileNames)
                    {
                        var fullPath = Path.Combine(mediaFolder, fileName);

                        if (File.Exists(fullPath)) return fullPath;
                    }
                }
            }

            return null;
        }

        public static void ClearDirectoryCache() => _directoryCache.Clear();

        public static void InvalidateDirectoryCache(string basePath) => _directoryCache.TryRemove(basePath, out _);
    }
}