using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper.Helpers
{
    internal static class ZipFileHelper
    {
        public static async Task<ZipFileInfo> GetZipFileInfoAsync(string romPath, bool isArcade, CancellationToken ct, Action<int>? progressCallback = null)
        {
            string fileName = Path.GetFileName(romPath);

            if (isArcade)
            {
                var fileInfo = new FileInfo(romPath);

                return new ZipFileInfo
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    Crc = await CrcCalculator.CalculateCrc32Async(romPath, progressCallback, ct)
                };
            }

            if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var fileInfo = new FileInfo(romPath);

                return new ZipFileInfo
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    Crc = await CrcCalculator.CalculateCrc32Async(romPath, progressCallback, ct)
                };
            }

            if (!ScreenScraperConfig.Instance.UseZipInternalFileName)
            {
                var fileInfo = new FileInfo(romPath);

                return new ZipFileInfo
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    Crc = await CrcCalculator.CalculateCrc32Async(romPath, progressCallback, ct)
                };
            }

            try
            {
                using var archive = ZipFile.OpenRead(romPath);
                var entry = archive.Entries.FirstOrDefault(static e => !string.IsNullOrEmpty(e.Name) && !e.FullName.EndsWith('/'));

                if (entry == null)
                {
                    var fileInfo = new FileInfo(romPath);

                    return new ZipFileInfo
                    {
                        FileName = fileName,
                        FileSize = fileInfo.Length,
                        Crc = await CrcCalculator.CalculateCrc32Async(romPath, progressCallback, ct)
                    };
                }

                using var stream = entry.Open();
                var crc = await CrcCalculator.CalculateCrc32FromStreamAsync(stream, entry.Length, progressCallback, ct);

                return new ZipFileInfo
                {
                    FileName = entry.Name,
                    FileSize = entry.Length,
                    Crc = crc
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ZIP 파일 처리 오류: {ex.Message}");
                var fileInfo = new FileInfo(romPath);

                return new ZipFileInfo
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    Crc = await CrcCalculator.CalculateCrc32Async(romPath, progressCallback, ct)
                };
            }
        }
    }
}