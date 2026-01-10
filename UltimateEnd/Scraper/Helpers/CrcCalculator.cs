using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UltimateEnd.Scraper.Helpers
{
    public static class CrcCalculator
    {
        private const int BufferSize = 256 * 1024; // 256KB

        public static async Task<string?> CalculateCrc32Async(string filePath, CancellationToken ct = default)
        {
            try
            {
                if (!File.Exists(filePath)) return null;

                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: BufferSize,
                    useAsync: true
                );

                return await CalculateCrc32FromStreamAsync(stream, ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRC 계산 오류 ({filePath}): {ex.Message}");
                return null;
            }
        }

        public static async Task<string?> CalculateCrc32FromStreamAsync(Stream stream, CancellationToken ct = default)
        {
            try
            {
                var crc32 = new System.IO.Hashing.Crc32();
                var buffer = new byte[BufferSize];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    crc32.Append(buffer.AsSpan(0, bytesRead));
                }

                var hash = crc32.GetCurrentHash();
                Array.Reverse(hash);

                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRC 스트림 계산 오류: {ex.Message}");
                return null;
            }
        }
    }
}