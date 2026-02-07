using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace UltimateEnd.Extractor
{
    public class WiiMetadataExtractor : IMetadataExtractor
    {
        private static readonly ConcurrentDictionary<string, ExtractorMetadata> _cache = new();

        public async Task<ExtractorMetadata> Extract(string filePath)
        {
            if (_cache.TryGetValue(filePath, out var cached)) return cached;

            var ext = Path.GetExtension(filePath).ToLower();

            var metadata = ext switch
            {
                ".wbfs" => await ExtractFromWbfs(filePath),
                ".wad" => await ExtractFromWad(filePath),
                ".iso" => await ExtractFromIso(filePath),
                ".gcm" => await ExtractFromIso(filePath),
                _ => null,
            };

            if (metadata != null) _cache[filePath] = metadata;

            return metadata;
        }

        private static async Task<ExtractorMetadata> ExtractFromWbfs(string wbfsPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(wbfsPath);
                    using var reader = new BinaryReader(stream);

                    stream.Seek(0x00, SeekOrigin.Begin);

                    byte[] magic = reader.ReadBytes(4);

                    if (Encoding.ASCII.GetString(magic) != "WBFS") return null;

                    uint hdSectorCount = ReadBigEndianUInt32(reader);
                    byte hdSectorShift = reader.ReadByte();
                    byte wbfsSectorShift = reader.ReadByte();
                    long hdSectorSize = 1L << hdSectorShift;
                    long wbfsSectorSize = 1L << wbfsSectorShift;

                    stream.Seek(0x200, SeekOrigin.Begin);

                    var metadata = ExtractFromDiscHeader(reader);
                    const int WII_DISC_HEADER_SIZE = 256;

                    stream.Seek(0x10, SeekOrigin.Begin);

                    byte firstDiscSlot = reader.ReadByte();
                    long discInfoSize = (WII_DISC_HEADER_SIZE + 2242 * 2 + hdSectorSize - 1) / hdSectorSize * hdSectorSize;
                    long sectorTableOffset = firstDiscSlot * wbfsSectorSize + hdSectorSize + WII_DISC_HEADER_SIZE;

                    stream.Seek(sectorTableOffset, SeekOrigin.Begin);

                    const long WII_SECTOR_SIZE = 0x8000;
                    const long WII_SECTOR_COUNT = 143432 * 2;
                    int blocksPerDisc = (int)((WII_SECTOR_COUNT * WII_SECTOR_SIZE + wbfsSectorSize - 1) / wbfsSectorSize);

                    stream.Seek(0x300, SeekOrigin.Begin);

                    byte[] rawTable = reader.ReadBytes(40);

                    stream.Seek(0x200000, SeekOrigin.Begin);

                    byte[] block1Start = reader.ReadBytes(16);

                    stream.Seek(0x200424, SeekOrigin.Begin);

                    byte[] fstData = reader.ReadBytes(8);
                    ushort[] wlbaTable = new ushort[blocksPerDisc];

                    for (int i = 0; i < blocksPerDisc && stream.Position + 2 <= stream.Length; i++)
                    {
                        byte b1 = reader.ReadByte();
                        byte b2 = reader.ReadByte();
                        wlbaTable[i] = (ushort)(b1 | (b2 << 8));
                    }

                    wlbaTable[0] = 1;
                    wlbaTable[1] = 1;

                    var wbfsStream = new WbfsVirtualStream(stream, wlbaTable, wbfsSectorSize, wbfsSectorShift);
                    var wbfsReader = new BinaryReader(wbfsStream);

                    try
                    {
                        metadata.Image = ExtractBannerImage(wbfsReader); 

                    }
                    catch { }

                    return metadata;
                }
                catch
                {
                    return null;
                }
            });
        }

        private static async Task<ExtractorMetadata> ExtractFromIso(string isoPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(isoPath);
                    using var reader = new BinaryReader(stream);

                    stream.Seek(0x00, SeekOrigin.Begin);

                    var metadata = ExtractFromDiscHeader(reader);

                    try
                    {
                        metadata.Image = ExtractBannerImage(reader);
                    }
                    catch { }

                    return metadata;
                }
                catch
                {
                    return null;
                }
            });
        }

        private static async Task<ExtractorMetadata> ExtractFromWad(string wadPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(wadPath);
                    using var reader = new BinaryReader(stream);
                    var metadata = new ExtractorMetadata();

                    stream.Seek(0x00, SeekOrigin.Begin);

                    uint headerSize = ReadBigEndianUInt32(reader);
                    uint type = ReadBigEndianUInt32(reader);
                    uint certChainSize = ReadBigEndianUInt32(reader);
                    uint reserved = ReadBigEndianUInt32(reader);
                    uint ticketSize = ReadBigEndianUInt32(reader);
                    uint tmdSize = ReadBigEndianUInt32(reader);
                    long tmdOffset = RoundUp(0x40 + certChainSize, 64) + RoundUp(ticketSize, 64);

                    stream.Seek(tmdOffset + 0x18C, SeekOrigin.Begin);

                    byte[] titleIdBytes = reader.ReadBytes(8);
                    string titleId = Encoding.ASCII.GetString(titleIdBytes, 4, 4);

                    metadata.Title = $"Channel {titleId}";
                    metadata.Developer = "Unknown";

                    return metadata;
                }
                catch
                {
                    return null;
                }
            });
        }

        private static ExtractorMetadata ExtractFromDiscHeader(BinaryReader reader)
        {
            var metadata = new ExtractorMetadata();

            try
            {
                var currentPos = reader.BaseStream.Position;
                byte[] gameIdBytes = reader.ReadBytes(6);
                string gameId = Encoding.ASCII.GetString(gameIdBytes);

                reader.BaseStream.Seek(currentPos + 0x20, SeekOrigin.Begin);

                byte[] titleBytes = reader.ReadBytes(64);
                int nullIndex = Array.IndexOf(titleBytes, (byte)0);

                if (nullIndex >= 0) Array.Resize(ref titleBytes, nullIndex);

                string title = Encoding.UTF8.GetString(titleBytes).Trim();

                metadata.Title = title;
                reader.BaseStream.Seek(currentPos + 0x04, SeekOrigin.Begin);

                byte[] makerBytes = reader.ReadBytes(2);
                string makerCode = Encoding.ASCII.GetString(makerBytes);

                metadata.Developer = GetDeveloperFromMakerCode(makerCode);
            }
            catch
            {
                if (string.IsNullOrEmpty(metadata.Title)) metadata.Title = "Extraction Failed";
            }

            return metadata;
        }

        private static byte[] ExtractBannerImage(BinaryReader reader)
        {
            try
            {
                reader.BaseStream.Seek(0x424, SeekOrigin.Begin);

                byte[] fstOffsetBytes = reader.ReadBytes(4);
                byte[] fstSizeBytes = reader.ReadBytes(4);

                if (fstOffsetBytes.Length < 4 || fstSizeBytes.Length < 4) return null;

                Array.Reverse(fstOffsetBytes);
                Array.Reverse(fstSizeBytes);
                uint fstOffset = BitConverter.ToUInt32(fstOffsetBytes, 0);
                uint fstSize = BitConverter.ToUInt32(fstSizeBytes, 0);                

                if (fstOffset == 0 || fstSize == 0) return null;

                long fstPosition = (long)fstOffset << 2;

                if (fstPosition >= reader.BaseStream.Length) return null;

                reader.BaseStream.Seek(fstPosition, SeekOrigin.Begin);

                byte rootType = reader.ReadByte();
                byte[] rootNameOffset = reader.ReadBytes(3);
                uint rootParentOffset = ReadBigEndianUInt32(reader);
                uint totalEntries = ReadBigEndianUInt32(reader);

                if (rootType != 1 || totalEntries == 0 || totalEntries > 10000) return null;

                long stringTableOffset = fstPosition + (totalEntries * 12);

                for (uint i = 1; i < totalEntries; i++)
                {
                    long entryOffset = fstPosition + (i * 12);

                    reader.BaseStream.Seek(entryOffset, SeekOrigin.Begin);

                    byte entryType = reader.ReadByte();
                    byte[] nameOffsetBytes = reader.ReadBytes(3);
                    uint nameOffset = (uint)((nameOffsetBytes[0] << 16) | (nameOffsetBytes[1] << 8) | nameOffsetBytes[2]);
                    uint fileOffset = ReadBigEndianUInt32(reader);
                    uint fileSize = ReadBigEndianUInt32(reader);

                    if (entryType == 1) continue;

                    long namePosition = stringTableOffset + nameOffset;

                    if (namePosition >= reader.BaseStream.Length) continue;

                    reader.BaseStream.Seek(namePosition, SeekOrigin.Begin);

                    string fileName = ReadNullTerminatedString(reader);

                    if (fileName.Equals("opening.bnr", StringComparison.OrdinalIgnoreCase))
                    {
                        long fileDataOffset = (long)fileOffset << 2;

                        if (fileDataOffset >= reader.BaseStream.Length) return null;

                        reader.BaseStream.Seek(fileDataOffset, SeekOrigin.Begin);

                        byte[] bannerData = reader.ReadBytes((int)fileSize);

                        return ExtractImageFromBanner(bannerData);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ExtractImageFromBanner(byte[] bannerData)
        {
            try
            {
                if (bannerData == null || bannerData.Length < 0x20) return null;

                string magic = Encoding.ASCII.GetString(bannerData, 0, Math.Min(4, bannerData.Length));

                if (magic == "IMET")
                {
                    int iconOffset = 0x20 + 0x20;
                    int iconSize = 96 * 48 * 2;

                    if (bannerData.Length >= iconOffset + iconSize)
                    {
                        byte[] iconData = new byte[iconSize];
                        Array.Copy(bannerData, iconOffset, iconData, 0, iconSize);

                        return ConvertRGB5A3ToPNG(iconData, 96, 48);
                    }
                }
                else if (magic == "BNR1" || magic == "BNR2")
                {
                    int iconOffset = 0x20;
                    int iconSize = 96 * 32 * 2;

                    if (bannerData.Length >= iconOffset + iconSize)
                    {
                        byte[] iconData = new byte[iconSize];
                        Array.Copy(bannerData, iconOffset, iconData, 0, iconSize);

                        return ConvertRGB5A3ToPNG(iconData, 96, 32);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ConvertRGB5A3ToPNG(byte[] rgb5a3Data, int width, int height)
        {
            try
            {
                byte[] rgbaData = new byte[width * height * 4];

                for (int ty = 0; ty < height; ty += 4)
                {
                    for (int tx = 0; tx < width; tx += 4)
                    {
                        for (int py = 0; py < 4; py++)
                        {
                            for (int px = 0; px < 4; px++)
                            {
                                int y = ty + py;
                                int x = tx + px;

                                if (x >= width || y >= height) continue;

                                int tileIndex = ((ty / 4) * (width / 4) + (tx / 4)) * 32 + (py * 4 + px) * 2;

                                if (tileIndex + 1 >= rgb5a3Data.Length) continue;

                                ushort pixel = (ushort)((rgb5a3Data[tileIndex] << 8) | rgb5a3Data[tileIndex + 1]);

                                int dstIndex = (y * width + x) * 4;

                                if ((pixel & 0x8000) != 0)
                                {
                                    int r = ((pixel >> 10) & 0x1F) << 3;
                                    int g = ((pixel >> 5) & 0x1F) << 3;
                                    int b = (pixel & 0x1F) << 3;

                                    rgbaData[dstIndex] = (byte)r;
                                    rgbaData[dstIndex + 1] = (byte)g;
                                    rgbaData[dstIndex + 2] = (byte)b;
                                    rgbaData[dstIndex + 3] = 255;
                                }
                                else
                                {
                                    int a = ((pixel >> 12) & 0x7) << 5;
                                    int r = ((pixel >> 8) & 0xF) << 4;
                                    int g = ((pixel >> 4) & 0xF) << 4;
                                    int b = (pixel & 0xF) << 4;

                                    rgbaData[dstIndex] = (byte)r;
                                    rgbaData[dstIndex + 1] = (byte)g;
                                    rgbaData[dstIndex + 2] = (byte)b;
                                    rgbaData[dstIndex + 3] = (byte)a;
                                }
                            }
                        }
                    }
                }

                return ConvertRGBAToPNG(rgbaData, width, height);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ConvertRGBAToPNG(byte[] rgbaData, int width, int height)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
            WriteChunk(writer, "IHDR", CreateIHDR(width, height));

            byte[] compressedData = CompressImageData(rgbaData, width, height);

            WriteChunk(writer, "IDAT", compressedData);
            WriteChunk(writer, "IEND", []);

            return ms.ToArray();
        }

        private static byte[] CreateIHDR(int width, int height)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            WriteBigEndian(writer, (uint)width);
            WriteBigEndian(writer, (uint)height);

            writer.Write((byte)8);
            writer.Write((byte)6);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((byte)0);

            return ms.ToArray();
        }

        private static byte[] CompressImageData(byte[] rgbaData, int width, int height)
        {
            using var ms = new MemoryStream();

            ms.WriteByte(0x78);
            ms.WriteByte(0x9C);

            int stride = width * 4;

            for (int y = 0; y < height; y++)
            {
                bool isLast = (y == height - 1);
                int blockSize = stride + 1;

                ms.WriteByte((byte)(isLast ? 0x01 : 0x00));
                ms.WriteByte((byte)(blockSize & 0xFF));
                ms.WriteByte((byte)((blockSize >> 8) & 0xFF));
                ms.WriteByte((byte)(~blockSize & 0xFF));
                ms.WriteByte((byte)((~blockSize >> 8) & 0xFF));
                ms.WriteByte(0);
                ms.Write(rgbaData, y * stride, stride);
            }

            uint adler = CalculateAdler32(rgbaData);

            ms.WriteByte((byte)((adler >> 24) & 0xFF));
            ms.WriteByte((byte)((adler >> 16) & 0xFF));
            ms.WriteByte((byte)((adler >> 8) & 0xFF));
            ms.WriteByte((byte)(adler & 0xFF));

            return ms.ToArray();
        }

        private static void WriteChunk(BinaryWriter writer, string type, byte[] data)
        {
            WriteBigEndian(writer, (uint)data.Length);

            byte[] typeBytes = Encoding.ASCII.GetBytes(type);

            writer.Write(typeBytes);
            writer.Write(data);

            uint crc = CalculateCRC(typeBytes, data);

            WriteBigEndian(writer, crc);
        }

        private static void WriteBigEndian(BinaryWriter writer, uint value)
        {
            writer.Write((byte)((value >> 24) & 0xFF));
            writer.Write((byte)((value >> 16) & 0xFF));
            writer.Write((byte)((value >> 8) & 0xFF));
            writer.Write((byte)(value & 0xFF));
        }

        private static uint CalculateCRC(byte[] typeBytes, byte[] data)
        {
            uint crc = 0xFFFFFFFF;

            foreach (byte b in typeBytes) crc = UpdateCRC(crc, b);

            foreach (byte b in data) crc = UpdateCRC(crc, b);

            return crc ^ 0xFFFFFFFF;
        }

        private static uint UpdateCRC(uint crc, byte b)
        {
            uint c = crc ^ b;

            for (int i = 0; i < 8; i++)
            {
                if ((c & 1) != 0) c = 0xEDB88320 ^ (c >> 1);
                else c >>= 1;
            }

            return c;
        }

        private static uint CalculateAdler32(byte[] data)
        {
            uint a = 1, b = 0;

            foreach (byte by in data)
            {
                a = (a + by) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }

        private static string ReadNullTerminatedString(BinaryReader reader)
        {
            var bytes = new System.Collections.Generic.List<byte>();
            byte b;

            while ((b = reader.ReadByte()) != 0)
            {
                bytes.Add(b);

                if (bytes.Count > 256) break;
            }

            return Encoding.ASCII.GetString([.. bytes]);
        }

        private static string GetDeveloperFromMakerCode(string makerCode)
        {
            return makerCode switch
            {
                "01" => "Nintendo",
                "08" => "Capcom",
                "41" => "Ubisoft",
                "4F" => "Eidos",
                "51" => "Acclaim",
                "52" => "Activision",
                "5D" => "Midway",
                "5G" => "Hudson",
                "64" => "LucasArts",
                "69" => "Electronic Arts",
                "6S" => "TDK",
                "78" => "THQ",
                "8P" => "Sega",
                "A4" => "Konami",
                "AF" => "Namco",
                "B2" => "Bandai",
                "DA" => "Tomy",
                "EB" => "Atlus",
                "E7" => "Kemco",
                "G9" => "Take-Two Interactive",
                _ => "Unknown Publisher"
            };
        }

        private static uint ReadBigEndianUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);

            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);

            return BitConverter.ToUInt32(bytes, 0);
        }

        private static long RoundUp(long value, long alignment) => (value + alignment - 1) / alignment * alignment;

        public static void ClearCache() => _cache.Clear();
    }
}