using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UltimateEnd.SaveFile.Dolphin
{
    public static class GameCubeIdExtractor
    {
        private const uint GCZ_MAGIC = 0xB10BC001;

        public static string? ExtractGameId(string isoPath)
        {
            if (!File.Exists(isoPath)) return null;

            string ext = Path.GetExtension(isoPath).ToLower();

            if (ext == ".gcz") return ExtractFromGcz(isoPath);

            return ExtractFromIso(isoPath);
        }

        private static string? ExtractFromIso(string isoPath)
        {
            try
            {
                using var stream = File.OpenRead(isoPath);
                byte[] gameIdBytes = new byte[6];
                stream.ReadExactly(gameIdBytes, 0, 6);
                string gameId = Encoding.ASCII.GetString(gameIdBytes);

                if (string.IsNullOrWhiteSpace(gameId) || gameId[0] != 'G') return null;

                return gameId;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractFromGcz(string gczPath)
        {
            try
            {
                using var stream = File.OpenRead(gczPath);

                byte[] headerBytes = new byte[32];

                if (stream.Read(headerBytes, 0, 32) != 32) return null;

                uint magic = BitConverter.ToUInt32(headerBytes, 0);
                ulong compressedDataSize = BitConverter.ToUInt64(headerBytes, 8);
                uint blockSize = BitConverter.ToUInt32(headerBytes, 24);
                uint numBlocks = BitConverter.ToUInt32(headerBytes, 28);

                if (magic != GCZ_MAGIC || numBlocks == 0) return null;

                byte[] offsetBytes = new byte[8];

                if (stream.Read(offsetBytes, 0, 8) != 8) return null;

                ulong firstBlockPointer = BitConverter.ToUInt64(offsetBytes, 0);
                bool isUncompressed = (firstBlockPointer & 0x8000000000000000UL) != 0;
                ulong actualOffset = firstBlockPointer & 0x7FFFFFFFFFFFFFFFUL;
                long dataOffset = 32 + (8 * numBlocks) + (4 * numBlocks);
                long absoluteBlockOffset = dataOffset + (long)actualOffset;

                stream.Seek(absoluteBlockOffset, SeekOrigin.Begin);

                byte[] decompressedData = new byte[blockSize];

                if (isUncompressed)
                {
                    if (stream.Read(decompressedData, 0, (int)blockSize) < 6) return null;
                }
                else
                {
                    long currentPos = stream.Position;
                    stream.Seek(32 + 8, SeekOrigin.Begin);

                    byte[] secondOffsetBytes = new byte[8];
                    ulong compressedSize;

                    if (stream.Read(secondOffsetBytes, 0, 8) == 8)
                    {
                        ulong secondBlockPointer = BitConverter.ToUInt64(secondOffsetBytes, 0);
                        ulong secondActualOffset = secondBlockPointer & 0x7FFFFFFFFFFFFFFFUL;
                        compressedSize = secondActualOffset - actualOffset;
                    }
                    else
                        compressedSize = compressedDataSize - actualOffset;

                    stream.Seek(currentPos, SeekOrigin.Begin);

                    byte[] compressedBlock = new byte[compressedSize];

                    if (stream.Read(compressedBlock, 0, (int)compressedSize) != (int)compressedSize) return null;

                    try
                    {
                        using var memStream = new MemoryStream(compressedBlock);
                        memStream.ReadByte();
                        memStream.ReadByte();

                        using var deflateStream = new DeflateStream(memStream, CompressionMode.Decompress);
                        int bytesRead = deflateStream.Read(decompressedData, 0, (int)blockSize);

                        if (bytesRead < 6) return null;
                    }
                    catch
                    {
                        return null;
                    }
                }

                string gameId = Encoding.ASCII.GetString(decompressedData, 0, 6);

                if (string.IsNullOrWhiteSpace(gameId) || gameId[0] != 'G') return null;

                return gameId;
            }
            catch
            {
                return null;
            }
        }

        public static string? ExtractGameIdFromGci(string gciFilePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(gciFilePath);
                var parts = fileName.Split('-');

                if (parts.Length >= 2)
                {
                    string gameId = parts[1].Trim();

                    if (gameId.Length >= 4 && gameId.Length <= 6) return gameId;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public static string[] FindGciFiles(string dolphinBasePath, string gameId)
        {
            var results = new System.Collections.Generic.List<string>();
            string gcPath = Path.Combine(dolphinBasePath, "GC");

            if (!Directory.Exists(gcPath)) return [.. results];

            string[] regions = ["USA", "EUR", "JAP"];
            string[] cards = ["Card A", "Card B"];
            string searchPattern = gameId.Length >= 4 ? gameId[..4] : gameId;

            foreach (var region in regions)
            {
                foreach (var card in cards)
                {
                    var cardPath = Path.Combine(gcPath, region, card);

                    if (!Directory.Exists(cardPath)) continue;

                    var gciFiles = Directory.GetFiles(cardPath, "*.gci");

                    foreach (var gciFile in gciFiles)
                    {
                        var extractedId = ExtractGameIdFromGci(gciFile);

                        if (extractedId != null && extractedId.StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase))
                            results.Add(gciFile);
                    }
                }
            }

            return [.. results];
        }

        public static string GetRegion(string gameId)
        {
            if (gameId.Length < 4) return "USA";

            char regionChar = gameId[3];
            return regionChar switch
            {
                'E' => "USA",
                'P' => "EUR",
                'J' => "JAP",
                _ => "USA"
            };
        }
    }
}