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
        public static async Task<ZipFileInfo> GetZipFileInfoAsync(string romPath, bool isArcade, CancellationToken ct)
        {
            string fileName = Path.GetFileName(romPath);

            // 아케이드는 항상 ZIP 파일 자체 정보
            if (isArcade)
            {
                var fileInfo = new FileInfo(romPath);
                return new ZipFileInfo
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    Crc = await CrcCalculator.CalculateCrc32Async(romPath, ct)
                };
            }

            // ZIP 파일이 아니면 그대로
            if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                var fileInfo = new FileInfo(romPath);
                return new ZipFileInfo
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    Crc = await CrcCalculator.CalculateCrc32Async(romPath, ct)
                };
            }

            // UseZipInternalFileName 옵션이 false면 ZIP 파일 자체 정보
            if (!ScreenScraperConfig.Instance.UseZipInternalFileName)
            {
                var fileInfo = new FileInfo(romPath);
                return new ZipFileInfo
                {
                    FileName = fileName,
                    FileSize = fileInfo.Length,
                    Crc = await CrcCalculator.CalculateCrc32Async(romPath, ct)
                };
            }

            // ZIP 내부 파일 정보 (한 번만 열기)
            try
            {
                using var archive = ZipFile.OpenRead(romPath);
                var entry = archive.Entries.FirstOrDefault(static e =>
                    !string.IsNullOrEmpty(e.Name) &&
                    !e.FullName.EndsWith('/'));

                if (entry == null)
                {
                    var fileInfo = new FileInfo(romPath);
                    return new ZipFileInfo
                    {
                        FileName = fileName,
                        FileSize = fileInfo.Length,
                        Crc = await CrcCalculator.CalculateCrc32Async(romPath, ct)
                    };
                }

                using var stream = entry.Open();
                var crc = await CrcCalculator.CalculateCrc32FromStreamAsync(stream, ct);

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
                    Crc = await CrcCalculator.CalculateCrc32Async(romPath, ct)
                };
            }
        }
    }

    internal class ZipFileInfo
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string Crc { get; set; }
    }
}