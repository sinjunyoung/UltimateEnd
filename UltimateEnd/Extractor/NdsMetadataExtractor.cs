using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UltimateEnd.Extractor
{
    public class NdsMetadataExtractor : IMetadataExtractor
    {
        private static readonly ConcurrentDictionary<string, ExtractorMetadata> _cache = new();
        private const int MaxCacheSize = 1000;

        private const int ICON_OFFSET_LOCATION = 0x68;
        private const int ICON_BITMAP_OFFSET = 0x20;
        private const int ICON_PALETTE_OFFSET = 0x220;
        private const int ICON_WIDTH = 32;
        private const int ICON_HEIGHT = 32;
        private const int BANNER_TITLE_JAPANESE_OFFSET = 0x240;
        private const int BANNER_TITLE_ENGLISH_OFFSET = 0x340;
        private const int BANNER_TITLE_LENGTH = 256;

        public async Task<ExtractorMetadata> Extract(string filePath)
        {
            if (_cache.TryGetValue(filePath, out var cached)) return cached;

            var extension = Path.GetExtension(filePath).ToLower();
            ExtractorMetadata metadata = null;

            if (extension == ".nds")
                metadata = await ExtractFromNDS(filePath);
            else if (extension == ".zip")
                metadata = await ExtractFromZip(filePath);

            if (metadata != null)
            {
                if (_cache.Count >= MaxCacheSize) _cache.Clear();
                _cache[filePath] = metadata;
            }

            return metadata;
        }

        private static async Task<ExtractorMetadata> ExtractFromNDS(string ndsPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var fileStream = File.OpenRead(ndsPath);
                    var headerBuffer = new byte[0x100];

                    fileStream.ReadExactly(headerBuffer, 0, 0x100);

                    var bannerOffset = BitConverter.ToUInt32(headerBuffer, ICON_OFFSET_LOCATION);

                    if (bannerOffset == 0 || bannerOffset > fileStream.Length) return null;

                    var requiredSize = bannerOffset + 0x2400;
                    var maxRead = Math.Min(fileStream.Length, requiredSize);
                    var buffer = new byte[maxRead];

                    fileStream.Position = 0;
                    fileStream.ReadExactly(buffer, 0, (int)maxRead);

                    using var ms = new MemoryStream(buffer);

                    return InternalProcessNDS(ms);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NDS File Error: {ex.Message}");
                    return null;
                }
            });
        }

        private static async Task<ExtractorMetadata> ExtractFromZip(string zipPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
                    var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nds", StringComparison.OrdinalIgnoreCase));

                    if (entry == null) return null;

                    using var entryStream = entry.Open();                    
                    var headerBuffer = new byte[0x100];

                    entryStream.ReadExactly(headerBuffer, 0, 0x100);

                    var bannerOffset = BitConverter.ToUInt32(headerBuffer, ICON_OFFSET_LOCATION);

                    if (bannerOffset == 0 || bannerOffset > entry.Length) return null;

                    var requiredSize = bannerOffset + 0x2400;
                    var maxRead = Math.Min(entry.Length, requiredSize);
                    using var entryStream2 = entry.Open();
                    var buffer = new byte[maxRead];

                    entryStream2.ReadExactly(buffer, 0, (int)maxRead);

                    using var ms = new MemoryStream(buffer);

                    return InternalProcessNDS(ms);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Zip Extraction Error: {ex.Message}");
                    return null;
                }
            });
        }

        private static ExtractorMetadata InternalProcessNDS(Stream stream)
        {
            try
            {
                using var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: true);
                var metadata = new ExtractorMetadata();

                stream.Seek(ICON_OFFSET_LOCATION, SeekOrigin.Begin);
                var bannerOffset = reader.ReadUInt32();

                if (bannerOffset > 0 && bannerOffset < stream.Length)
                {
                    ExtractTitle(reader, bannerOffset, metadata, BANNER_TITLE_ENGLISH_OFFSET);

                    if (string.IsNullOrWhiteSpace(metadata.Title)) ExtractTitle(reader, bannerOffset, metadata, BANNER_TITLE_JAPANESE_OFFSET);

                    var iconData = ExtractIcon(reader, bannerOffset);
                    metadata.CoverImage = iconData;
                    metadata.LogoImage = iconData;
                }

                return metadata;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Internal Processing Error: {ex.Message}");
                return null;
            }
        }

        private static void ExtractTitle(BinaryReader reader, uint bannerOffset, ExtractorMetadata metadata, int offset)
        {
            reader.BaseStream.Seek(bannerOffset + offset, SeekOrigin.Begin);
            var titleBytes = reader.ReadBytes(BANNER_TITLE_LENGTH);
            var rawTitle = Encoding.Unicode.GetString(titleBytes).TrimEnd('\0');

            var lines = rawTitle.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length > 0)
            {
                metadata.Title = lines[0].Trim();

                if (lines.Length > 1) metadata.Developer = lines[^1].Trim();
            }
        }

        private static byte[] ExtractIcon(BinaryReader reader, uint iconOffset)
        {
            try
            {
                reader.BaseStream.Seek(iconOffset + ICON_BITMAP_OFFSET, SeekOrigin.Begin);
                var bitmapData = reader.ReadBytes(512);

                reader.BaseStream.Seek(iconOffset + ICON_PALETTE_OFFSET, SeekOrigin.Begin);
                var paletteData = reader.ReadBytes(32);

                var palette = new uint[16];

                for (int i = 0; i < 16; i++)
                {
                    ushort color = (ushort)(paletteData[i << 1] | (paletteData[(i << 1) + 1] << 8));
                    byte r = (byte)((color & 0x1F) << 3);
                    byte g = (byte)(((color >> 5) & 0x1F) << 3);
                    byte b = (byte)(((color >> 10) & 0x1F) << 3);
                    byte a = i == 0 ? (byte)0 : (byte)255;

                    palette[i] = (uint)(a << 24 | b << 16 | g << 8 | r);
                }

                var imageData = new byte[ICON_WIDTH * ICON_HEIGHT * 4];

                for (int tileY = 0; tileY < 4; tileY++)
                {
                    for (int tileX = 0; tileX < 4; tileX++)
                    {
                        int tileIndex = tileY * 4 + tileX;
                        int tileOffset = tileIndex << 5;

                        for (int y = 0; y < 8; y++)
                        {
                            for (int x = 0; x < 8; x++)
                            {
                                int pixelIndex = y * 8 + x;
                                int byteIndex = pixelIndex >> 1;
                                int nibble = pixelIndex & 1;

                                byte colorIndex = nibble == 0 ? (byte)(bitmapData[tileOffset + byteIndex] & 0x0F) : (byte)((bitmapData[tileOffset + byteIndex] >> 4) & 0x0F);

                                int destX = tileX * 8 + x;
                                int destY = tileY * 8 + y;
                                int destIndex = (destY * ICON_WIDTH + destX) << 2;

                                uint color = palette[colorIndex];
                                imageData[destIndex] = (byte)(color & 0xFF);
                                imageData[destIndex + 1] = (byte)((color >> 8) & 0xFF);
                                imageData[destIndex + 2] = (byte)((color >> 16) & 0xFF);
                                imageData[destIndex + 3] = (byte)((color >> 24) & 0xFF);
                            }
                        }
                    }
                }

                return ConvertToPng(imageData, ICON_WIDTH, ICON_HEIGHT);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Icon Extraction Error: {ex.Message}");
                return null;
            }
        }

        private static byte[] ConvertToPng(byte[] rgbaData, int width, int height)
        {
            try
            {
                using var bitmap = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
                var pixelPtr = bitmap.GetPixels();
                Marshal.Copy(rgbaData, 0, pixelPtr, rgbaData.Length);

                using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

                return data.ToArray();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PNG Conversion Error: {ex.Message}");
                return null;
            }
        }

        public static void ClearCache() => _cache.Clear();
    }
}