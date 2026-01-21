using System.IO;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper.Helpers
{
    public static class CacheKeyBuilder
    {
        public static string Build(ScreenScraperSystemId systemId, string romPath, bool isArcade, string crc = null)
        {
            if (!string.IsNullOrEmpty(crc)) return $"crc_{(int)systemId}_{crc}";

            var fileName = BuildSearchFileName(romPath, isArcade);

            if (isArcade)
                return $"arcade_{(int)systemId}_{fileName}";

            return $"name_{(int)systemId}_{fileName}";
        }

        public static string BuildSearchFileName(string romPath, bool isArcade)
        {
            if (isArcade) return GetArcadeSearchFileName(romPath);

            string fileName = Path.GetFileName(romPath);

            if (ScreenScraperConfig.Instance.UseZipInternalFileName) fileName = TryExtractRomFileName(romPath) ?? fileName;

            return GameNameNormalizer.Normalize(fileName);
        }

        private static string GetArcadeSearchFileName(string romPath)
        {
            try
            {
                var dbGame = FbNeoGameDatabase.GetGameByPath(romPath);

                if (dbGame != null && !string.IsNullOrEmpty(dbGame.ParentRomFile)) return Path.GetFileNameWithoutExtension(dbGame.ParentRomFile);

                return Path.GetFileNameWithoutExtension(romPath);
            }
            catch
            {
                return Path.GetFileNameWithoutExtension(romPath);
            }
        }

        private static string? TryExtractRomFileName(string romPath)
        {
            try
            {
                var ext = Path.GetExtension(romPath).ToLowerInvariant();

                if (ext == ".zip")
                {
                    using var archive = System.IO.Compression.ZipFile.OpenRead(romPath);
                    var entry = archive.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Name));

                    return entry != null ? Path.GetFileNameWithoutExtension(entry.Name) : null;
                }

                return Path.GetFileNameWithoutExtension(romPath);
            }
            catch
            {
                return null;
            }
        }
    }
}