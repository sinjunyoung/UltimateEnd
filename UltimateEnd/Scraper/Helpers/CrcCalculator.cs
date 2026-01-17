using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UltimateEnd.Scraper.Helpers
{
    public static class CrcCalculator
    {
        private const int BufferSize = 256 * 1024;

        public static async Task<string?> CalculateCrc32Async(string filePath, Action<int>? progressCallback = null, CancellationToken ct = default)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: BufferSize, useAsync: true);
                long totalSize = stream.Length;

                return await CalculateCrc32FromStreamAsync(stream, totalSize, progressCallback, ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRC 계산 오류 ({filePath}): {ex.Message}");

                return null;
            }
        }

        public static async Task<string?> CalculateCrc32FromStreamAsync(Stream stream, long? totalSize = null, Action<int>? progressCallback = null, CancellationToken ct = default)
        {
            try
            {
                var crc32 = new System.IO.Hashing.Crc32();
                var buffer = new byte[BufferSize];
                int bytesRead;
                long totalBytesRead = 0;

                if (totalSize == null || totalSize == 0) totalSize = stream.Length;

                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    crc32.Append(buffer.AsSpan(0, bytesRead));
                    totalBytesRead += bytesRead;

                    if (progressCallback != null && totalSize > 0)
                    {
                        int percentage = (int)((totalBytesRead * 100) / totalSize.Value);
                        progressCallback(percentage);
                    }
                }

                var hash = crc32.GetCurrentHash();
                Array.Reverse(hash);

                return Convert.ToHexString(hash);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRC 스트림 계산 오류: {ex.Message}");
                return null;
            }
        }
    }
}