using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UltimateEnd.Extractor
{
    public class ThreeDSMetadataExtractor : IMetadataExtractor
    {
        private static readonly ConcurrentDictionary<string, GameMetadata> _cache = new();
        private static bool _cryptoInitialized = false;

        public ThreeDSMetadataExtractor(string aesKeysPath)
        {
            _cryptoInitialized = NCCHDecryption.Initialize(aesKeysPath);
        }

        public async Task<GameMetadata> Extract(string filePath)
        {
            if (_cache.TryGetValue(filePath, out var cached)) return cached;

            var ext = Path.GetExtension(filePath).ToLower();
            GameMetadata metadata = null;

            if (ext == ".3ds" || ext == ".cci")
                metadata = await ExtractFrom3DS(filePath);
            else if (ext == ".cia")
                metadata = await ExtractFromCIA(filePath);

            if (metadata != null) _cache[filePath] = metadata;

            return metadata;
        }

        private static async Task<GameMetadata> ExtractFrom3DS(string path)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var fs = File.OpenRead(path);
                    using var reader = new BinaryReader(fs);

                    long ncsdBase = 0;
                    fs.Seek(0, SeekOrigin.Begin);

                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "NCSD")
                    {
                        fs.Seek(0x100, SeekOrigin.Begin);

                        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) == "NCSD") ncsdBase = 0x100;
                    }

                    fs.Seek(ncsdBase + 0x120, SeekOrigin.Begin);
                    uint off0 = reader.ReadUInt32();
                    long ncchOffset = (off0 == 0) ? ncsdBase + 0x4000 : ncsdBase + (long)off0 * 0x200;

                    fs.Seek(ncchOffset + 0x100, SeekOrigin.Begin);

                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "NCCH")
                    {
                        ncchOffset = FindMagic(fs, "NCCH", ncsdBase, ncsdBase + 0x20000) - 0x100;

                        if (ncchOffset < -100) return ScanForSMDH(fs);
                    }

                    fs.Seek(ncchOffset + 0x18F, SeekOrigin.Begin);
                    byte cryptoFlags = reader.ReadByte();
                    bool isEncrypted = (cryptoFlags & 0x04) == 0;

                    if (!isEncrypted)
                    {
                        fs.Seek(ncchOffset + 0x1A0, SeekOrigin.Begin);
                        uint exefsOff = reader.ReadUInt32() * 0x200;
                        long exefsStart = ncchOffset + exefsOff;

                        fs.Seek(exefsStart, SeekOrigin.Begin);

                        for (int i = 0; i < 10; i++)
                        {
                            var entryName = Encoding.ASCII.GetString(reader.ReadBytes(8)).Replace("\0", "").Trim().ToLower();
                            uint entryOff = reader.ReadUInt32();
                            uint entryLen = reader.ReadUInt32();

                            if (entryName == "icon")
                            {
                                var res = ExtractSMDH(fs, exefsStart + 0x200 + entryOff);

                                if (res != null) return res;
                            }
                        }
                    }
                    else if (isEncrypted && _cryptoInitialized)
                    {
                        fs.Seek(ncchOffset + 0x1A0, SeekOrigin.Begin);
                        uint exefsOff = reader.ReadUInt32() * 0x200;
                        long exefsStart = ncchOffset + exefsOff;

                        fs.Seek(exefsStart, SeekOrigin.Begin);
                        var encryptedExeFS = reader.ReadBytes(64);

                        fs.Seek(ncchOffset, SeekOrigin.Begin);
                        var header = NCCHHeader.Read(reader);

                        using var decryptedStream = NCCHDecryption.CreateDecryptedStream(path, header, ncchOffset);

                        if (decryptedStream != null)
                        {
                            using var decReader = new BinaryReader(decryptedStream);

                            decryptedStream.Seek(exefsOff, SeekOrigin.Begin);

                            var decryptedExeFS = decReader.ReadBytes(64);
                            var asText = Encoding.ASCII.GetString(decryptedExeFS);

                            decryptedStream.Seek(exefsOff, SeekOrigin.Begin);

                            for (int i = 0; i < 10; i++)
                            {
                                var nameBytes = decReader.ReadBytes(8);
                                var entryName = Encoding.ASCII.GetString(nameBytes).Replace("\0", "").Trim().ToLower();
                                uint entryOff = decReader.ReadUInt32();
                                uint entryLen = decReader.ReadUInt32();
                                
                                if (entryName == "icon")
                                {
                                    var res = ExtractSMDH(decryptedStream, exefsStart + 0x200 + entryOff);
                                    if (res != null) return res;
                                }
                            }
                        }
                    }

                    return ScanForSMDH(fs);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static GameMetadata ScanForSMDH(FileStream fs)
        {
            long smdhPos = FindMagic(fs, "SMDH", 0, 10 * 1024 * 1024);

            if (smdhPos != -1) return ExtractSMDH(fs, smdhPos);

            return null;
        }

        private static long FindMagic(Stream fs, string magic, long start, long limit)
        {
            byte[] target = Encoding.ASCII.GetBytes(magic);
            byte[] buffer = new byte[target.Length];
            fs.Seek(start, SeekOrigin.Begin);

            while (fs.Position < limit && fs.Position < fs.Length)
            {
                int read = fs.Read(buffer, 0, buffer.Length);

                if (read < buffer.Length) break;

                if (buffer.SequenceEqual(target)) return fs.Position - target.Length;

                fs.Seek(-target.Length + 1, SeekOrigin.Current);
            }

            return -1;
        }

        private static async Task<GameMetadata> ExtractFromCIA(string path)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var fs = File.OpenRead(path);
                    using var reader = new BinaryReader(fs);

                    var headerSize = reader.ReadUInt32();

                    reader.ReadUInt16();
                    reader.ReadUInt16();

                    var certChainSize = reader.ReadUInt32();
                    var ticketSize = reader.ReadUInt32();
                    var tmdSize = reader.ReadUInt32();
                    var metaSize = reader.ReadUInt32();
                    var contentSize = reader.ReadUInt64();

                    static long Align64(long v) => (v + 63) & ~63L;

                    var contentOffset = Align64(headerSize) + Align64(certChainSize) +
                                       Align64(ticketSize) + Align64(tmdSize);
                    var metaOffset = Align64(contentOffset + (long)contentSize);

                    if (metaSize > 0) return ExtractSMDH(fs, metaOffset + 0x400);

                    return null;
                }
                catch { return null; }
            });
        }

        private static GameMetadata ExtractSMDH(Stream stream, long offset)
        {
            try
            {
                stream.Seek(offset, SeekOrigin.Begin);

                using var reader = new BinaryReader(stream, Encoding.UTF8, true);

                var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));

                if (magic != "SMDH") return null;

                var metadata = new GameMetadata();
                stream.Seek(offset + 8, SeekOrigin.Begin);

                var titles = new (string name, string pub)[16];

                for (int i = 0; i < 16; i++)
                {
                    var shortDesc = ReadUTF16String(reader, 0x80);
                    var longDesc = ReadUTF16String(reader, 0x100);
                    var publisher = ReadUTF16String(reader, 0x80);

                    titles[i] = (shortDesc, publisher);
                }

                var priority = new int[] { 7, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 0 };

                (string name, string pub) found = (string.Empty, string.Empty);

                foreach (var idx in priority)
                {
                    if (!string.IsNullOrWhiteSpace(titles[idx].name))
                    {
                        found = titles[idx];
                        break;
                    }
                }

                metadata.Title = found.name ?? "Unknown Title";
                metadata.Developer = found.pub ?? "Unknown Publisher";

                stream.Seek(offset + 0x24C0, SeekOrigin.Begin);

                var iconData = reader.ReadBytes(0x1200);

                metadata.CoverImage = ConvertTiledRGB565ToPNG(iconData, 48, 48);
                metadata.LogoImage = metadata.CoverImage;

                return metadata;
            }
            catch { return null; }
        }

        private static string ReadUTF16String(BinaryReader reader, int maxBytes)
        {
            var bytes = reader.ReadBytes(maxBytes);
            return Encoding.Unicode.GetString(bytes).Split('\0')[0].Trim();
        }

        private static byte[] ConvertTiledRGB565ToPNG(byte[] tiledData, int width, int height)
        {
            try
            {
                var rgba = new byte[width * height * 4];
                int index = 0;

                for (int tileY = 0; tileY < height; tileY += 8)
                {
                    for (int tileX = 0; tileX < width; tileX += 8)
                    {
                        for (int k = 0; k < 64; k++)
                        {
                            int x = 0, y = 0;

                            for (int bit = 0; bit < 3; bit++)
                            {
                                x |= ((k >> (bit * 2)) & 1) << bit;
                                y |= ((k >> (bit * 2 + 1)) & 1) << bit;
                            }

                            int pixelX = tileX + x;
                            int pixelY = tileY + y;

                            if (index + 1 >= tiledData.Length) continue;

                            ushort rgb565 = BitConverter.ToUInt16(tiledData, index);
                            index += 2;

                            byte r = (byte)(((rgb565 >> 11) & 0x1F) * 255 / 31);
                            byte g = (byte)(((rgb565 >> 5) & 0x3F) * 255 / 63);
                            byte b = (byte)((rgb565 & 0x1F) * 255 / 31);

                            int destIndex = (pixelY * width + pixelX) * 4;
                            if (destIndex + 3 < rgba.Length)
                            {
                                rgba[destIndex] = r;
                                rgba[destIndex + 1] = g;
                                rgba[destIndex + 2] = b;
                                rgba[destIndex + 3] = 255;
                            }
                        }
                    }
                }

                return ConvertRGBAToPNG(rgba, width, height);
            }
            catch
            {
                return null;
            }
        }

        private static byte[] ConvertRGBAToPNG(byte[] rgba, int width, int height)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

            WriteChunk(writer, "IHDR", w => {
                w.Write(SwapEndian((uint)width));
                w.Write(SwapEndian((uint)height));
                w.Write((byte)8); w.Write((byte)6); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
            });

            using var dataStream = new MemoryStream();

            for (int y = 0; y < height; y++)
            {
                dataStream.WriteByte(0);
                dataStream.Write(rgba, y * width * 4, width * 4);
            }

            var compressed = CompressZlib(dataStream.ToArray());

            WriteChunk(writer, "IDAT", w => w.Write(compressed));
            WriteChunk(writer, "IEND", w => { });

            return ms.ToArray();
        }

        private static void WriteChunk(BinaryWriter writer, string type, Action<BinaryWriter> writeData)
        {
            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            writeData(w);

            byte[] data = ms.ToArray();

            writer.Write(SwapEndian((uint)data.Length));

            byte[] typeBytes = Encoding.ASCII.GetBytes(type);

            writer.Write(typeBytes);
            writer.Write(data);
            writer.Write(SwapEndian(CalculateCRC([.. typeBytes, .. data])));
        }

        private static uint SwapEndian(uint v) =>
            (v >> 24) | (v << 24) | ((v >> 8) & 0x0000FF00) | ((v << 8) & 0x00FF0000);

        private static uint CalculateCRC(byte[] d)
        {
            uint c = 0xFFFFFFFF;

            foreach (var b in d)
            {
                c ^= b;

                for (int i = 0; i < 8; i++)
                    c = (c >> 1) ^ ((c & 1) != 0 ? 0xEDB88320 : 0);
            }

            return c ^ 0xFFFFFFFF;
        }

        private static byte[] CompressZlib(byte[] d)
        {
            using var outS = new MemoryStream();

            outS.WriteByte(0x78); outS.WriteByte(0x9C);

            using (var ds = new System.IO.Compression.DeflateStream(outS, CompressionMode.Compress, true))
                ds.Write(d, 0, d.Length);

            var a32 = CalculateAdler32(d);
            outS.Write(BitConverter.GetBytes(SwapEndian(a32)), 0, 4);

            return outS.ToArray();
        }

        private static uint CalculateAdler32(byte[] d)
        {
            uint a = 1, b = 0;

            foreach (var v in d)
            {
                a = (a + v) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }
    }
}