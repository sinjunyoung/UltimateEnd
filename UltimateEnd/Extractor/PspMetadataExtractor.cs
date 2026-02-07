using LibHac.Diag;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UltimateEnd.SaveFile.CHD;
using UltimateEnd.SaveFile.Parsers;

namespace UltimateEnd.Extractor
{
    public class PspMetadataExtractor : IMetadataExtractor
    {
        private static readonly ConcurrentDictionary<string, ExtractorMetadata> _cache = new();
        private static readonly byte[] PNG_SIGNATURE = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        public async Task<ExtractorMetadata> Extract(string filePath)
        {
            if (_cache.TryGetValue(filePath, out var cached)) return cached;

            var ext = Path.GetExtension(filePath).ToLower();
            var metadata = ext switch
            {
                ".iso" => await ExtractFromISO(filePath),
                ".cso" => await ExtractFromCSO(filePath),
                ".chd" => await ExtractFromCHD(filePath),
                _ => null,
            };

            if (metadata != null) _cache[filePath] = metadata;

            return metadata;
        }

        private static async Task<ExtractorMetadata> ExtractFromISO(string isoPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(isoPath);
                    return ExtractMetadataFromStream(stream);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static async Task<ExtractorMetadata> ExtractFromCSO(string csoPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var csoReader = new CsoStreamReader(csoPath);
                    return ExtractMetadataFromCsoReader(csoReader);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static ExtractorMetadata ExtractMetadataFromCsoReader(CsoStreamReader reader)
        {
            var metadata = new ExtractorMetadata();

            try
            {
                var pvd = reader.ReadSector(16);
                if (pvd != null && pvd.Length >= 2048)
                {
                    if (pvd[0] == 0x01 && pvd[1] == 0x43 && pvd[2] == 0x44 &&
                        pvd[3] == 0x30 && pvd[4] == 0x30 && pvd[5] == 0x31)
                    {
                        uint rootLBA = BitConverter.ToUInt32(pvd, 158);
                        uint pspGameLBA = FindDirectoryInCso(reader, rootLBA, "PSP_GAME");

                        if (pspGameLBA != 0)
                        {
                            var paramSfoInfo = FindFileInCso(reader, pspGameLBA, "PARAM.SFO");
                            if (paramSfoInfo.HasValue)
                            {
                                byte[] sfoData = ReadFileFromCso(reader, paramSfoInfo.Value.lba, paramSfoInfo.Value.size);
                                if (sfoData != null)
                                    ParseParamSfo(sfoData, metadata);
                            }

                            var iconInfo = FindFileInCso(reader, pspGameLBA, "ICON0.PNG");
                            if (iconInfo.HasValue)
                            {
                                byte[] iconData = ReadFileFromCso(reader, iconInfo.Value.lba, iconInfo.Value.size);
                                if (iconData != null && iconData.Length > 8 && IsPngSignature(iconData))
                                {
                                    metadata.LogoImage = iconData;
                                    metadata.CoverImage = iconData;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return metadata;
        }

        private static uint FindDirectoryInCso(CsoStreamReader reader, uint dirLBA, string dirName)
        {
            var sector = reader.ReadSector(dirLBA);
            if (sector == null) return 0;
            return FindDirectoryInSector(sector, dirName);
        }

        private static (uint lba, uint size)? FindFileInCso(CsoStreamReader reader, uint dirLBA, string fileName)
        {
            var sector = reader.ReadSector(dirLBA);
            if (sector == null) return null;
            return FindFileInSector(sector, fileName);
        }

        private static byte[] ReadFileFromCso(CsoStreamReader reader, uint startLBA, uint fileSize)
        {
            uint readSize = Math.Min(fileSize, 1024 * 1024);
            byte[] result = new byte[readSize];
            uint sectorsNeeded = (readSize + 2047) / 2048;

            for (uint i = 0; i < sectorsNeeded; i++)
            {
                var sector = reader.ReadSector(startLBA + i);
                if (sector == null) break;

                uint copySize = Math.Min(2048, readSize - i * 2048);
                Array.Copy(sector, 0, result, i * 2048, copySize);
            }

            return result;
        }

        private static async Task<ExtractorMetadata> ExtractFromCHD(string chdPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var chd = new LibChdrWrapper();

                    if (chd.Open(chdPath) != ChdrError.CHDERR_NONE || !chd.Header.HasValue) return null;

                    var header = chd.Header.Value;
                    uint unitbytes = header.unitbytes;
                    ulong unitcount = header.unitcount;

                    if (unitbytes == 0 || unitbytes > 10000)
                    {
                        var metadata = chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA2, 0) ??
                                      chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA, 0);

                        if (metadata != null && metadata.Contains("TYPE:MODE1"))
                            unitbytes = 2352;
                        else
                            unitbytes = 2048;
                    }

                    if (unitcount == 0) unitcount = header.logicalbytes / unitbytes;

                    var blockDevice = new ChdBlockDevice(chd, header.hunkbytes, unitbytes, (uint)unitcount);
                    return ExtractMetadataFromBlockDevice(blockDevice, chd, header);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static ExtractorMetadata ExtractMetadataFromStream(Stream stream)
        {
            var metadata = new ExtractorMetadata();
            try
            {
                if (TryParseIso9660Stream(stream, metadata))
                    return metadata;

                var paramSfoData = FindParamSfo(stream);
                if (paramSfoData != null)
                    ParseParamSfo(paramSfoData, metadata);

                stream.Seek(0, SeekOrigin.Begin);
                var icon = FindFirstPng(stream);
                if (icon != null)
                {
                    metadata.LogoImage = icon;
                    metadata.CoverImage = icon;
                }
            }
            catch { }

            return metadata;
        }

        private static bool TryParseIso9660Stream(Stream stream, ExtractorMetadata metadata)
        {
            try
            {
                stream.Seek(16 * 2048, SeekOrigin.Begin);
                byte[] pvd = new byte[2048];
                stream.ReadExactly(pvd, 0, 2048);

                if (pvd[0] != 0x01 || pvd[1] != 0x43 || pvd[2] != 0x44) return false;

                uint rootLBA = BitConverter.ToUInt32(pvd, 158);
                uint pspGameLBA = FindDirectoryInStream(stream, rootLBA, "PSP_GAME");

                if (pspGameLBA == 0) return false;

                var sfoInfo = FindFileInStream(stream, pspGameLBA, "PARAM.SFO");
                if (sfoInfo.HasValue)
                {
                    byte[] sfoData = ReadFileFromStream(stream, sfoInfo.Value.lba, sfoInfo.Value.size);
                    if (sfoData != null) ParseParamSfo(sfoData, metadata);
                }

                var iconInfo = FindFileInStream(stream, pspGameLBA, "ICON0.PNG");
                if (iconInfo.HasValue)
                {
                    var icon = ReadFileFromStream(stream, iconInfo.Value.lba, iconInfo.Value.size);
                    metadata.LogoImage = icon;
                    metadata.CoverImage = icon;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ExtractorMetadata ExtractMetadataFromBlockDevice(ChdBlockDevice device, LibChdrWrapper chd, ChdrHeader header)
        {
            var metadata = new ExtractorMetadata();

            try
            {
                var pvd = device.ReadBlock(16);
                if (pvd != null && pvd.Length >= 2048)
                {
                    if (pvd[0] == 0x01 && pvd[1] == 0x43 && pvd[2] == 0x44 &&
                        pvd[3] == 0x30 && pvd[4] == 0x30 && pvd[5] == 0x31)
                    {
                        uint rootLBA = BitConverter.ToUInt32(pvd, 158);
                        uint pspGameLBA = FindDirectory(device, rootLBA, "PSP_GAME");

                        if (pspGameLBA != 0)
                        {
                            var paramSfoInfo = FindFile(device, pspGameLBA, "PARAM.SFO");
                            if (paramSfoInfo.HasValue)
                            {
                                byte[] sfoData = ReadFile(device, paramSfoInfo.Value.lba, paramSfoInfo.Value.size);
                                if (sfoData != null)
                                    ParseParamSfo(sfoData, metadata);
                            }

                            var iconInfo = FindFile(device, pspGameLBA, "ICON0.PNG");
                            if (iconInfo.HasValue)
                            {
                                byte[] iconData = ReadFile(device, iconInfo.Value.lba, iconInfo.Value.size);
                                if (iconData != null && iconData.Length > 8 && IsPngSignature(iconData))
                                {
                                    metadata.LogoImage = iconData;
                                    metadata.CoverImage = iconData;
                                }
                            }
                        }
                    }
                }

                if (metadata.LogoImage == null)
                    FallbackFullScan(chd, header, metadata);
            }
            catch { }

            return metadata;
        }

        private static byte[] FindParamSfo(Stream stream)
        {
            var searchSize = Math.Min(50 * 1024 * 1024, stream.Length);
            var buffer = new byte[8192];
            stream.Seek(0, SeekOrigin.Begin);

            for (long offset = 0; offset < searchSize; offset += buffer.Length - 4)
            {
                stream.Seek(offset, SeekOrigin.Begin);
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead < 4) break;

                for (int i = 0; i < bytesRead - 4; i++)
                {
                    if (buffer[i] == 0x00 && buffer[i + 1] == 0x50 &&
                        buffer[i + 2] == 0x53 && buffer[i + 3] == 0x46)
                    {
                        stream.Seek(offset + i, SeekOrigin.Begin);
                        byte[] sfoData = new byte[10240];
                        int sfoSize = stream.Read(sfoData, 0, sfoData.Length);
                        if (sfoSize < sfoData.Length) Array.Resize(ref sfoData, sfoSize);
                        return sfoData;
                    }
                }
            }

            return null;
        }

        private static byte[] FindFirstPng(Stream stream)
        {
            var searchSize = Math.Min(100 * 1024 * 1024, stream.Length);
            var buffer = new byte[8192];

            for (long offset = 0; offset < searchSize; offset += buffer.Length - 8)
            {
                stream.Seek(offset, SeekOrigin.Begin);
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead < 8) break;

                for (int i = 0; i < bytesRead - 8; i++)
                {
                    if (IsPngSignature(buffer, i))
                    {
                        stream.Seek(offset + i, SeekOrigin.Begin);
                        byte[] iconData = new byte[1024 * 1024];
                        int iconSize = stream.Read(iconData, 0, iconData.Length);

                        int actualSize = FindPngEnd(iconData, iconSize);
                        if (actualSize > 0)
                        {
                            Array.Resize(ref iconData, actualSize);
                            return iconData;
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsPngSignature(byte[] data, int offset = 0)
        {
            for (int i = 0; i < PNG_SIGNATURE.Length; i++)
            {
                if (data[offset + i] != PNG_SIGNATURE[i])
                    return false;
            }
            return true;
        }

        private static int FindPngEnd(byte[] data, int maxSize)
        {
            for (int i = 0; i < maxSize - 12; i++)
            {
                if (data[i] == 0x00 && data[i + 1] == 0x00 && data[i + 2] == 0x00 && data[i + 3] == 0x00 &&
                    data[i + 4] == 0x49 && data[i + 5] == 0x45 && data[i + 6] == 0x4E && data[i + 7] == 0x44 &&
                    data[i + 8] == 0xAE && data[i + 9] == 0x42 && data[i + 10] == 0x60 && data[i + 11] == 0x82)
                {
                    return i + 12;
                }
            }

            return maxSize;
        }

        private static void ParseParamSfo(byte[] sfoData, ExtractorMetadata metadata)
        {
            try
            {
                if (sfoData.Length < 20) return;
                if (sfoData[0] != 0x00 || sfoData[1] != 0x50 || sfoData[2] != 0x53 || sfoData[3] != 0x46) return;

                var keyTableOffset = BitConverter.ToInt32(sfoData, 0x08);
                var dataTableOffset = BitConverter.ToInt32(sfoData, 0x0C);
                var entryCount = BitConverter.ToInt32(sfoData, 0x10);

                if (entryCount <= 0 || entryCount > 100) return;

                for (int i = 0; i < entryCount; i++)
                {
                    var entryOffset = 0x14 + i * 16;
                    if (entryOffset + 16 > sfoData.Length) break;

                    var keyOffset = BitConverter.ToInt16(sfoData, entryOffset + 0);
                    var dataOffset = BitConverter.ToInt32(sfoData, entryOffset + 12);

                    if (keyTableOffset + keyOffset >= sfoData.Length || dataTableOffset + dataOffset >= sfoData.Length)
                        continue;

                    var keyNameOffset = keyTableOffset + keyOffset;
                    var keyName = ReadNullTerminatedString(sfoData, keyNameOffset);

                    var valueOffset = dataTableOffset + dataOffset;

                    if (keyName == "DISC_ID")
                    {
                        var discId = ReadNullTerminatedString(sfoData, valueOffset);

                        if (!string.IsNullOrEmpty(discId))
                            metadata.Id = discId;
                    }

                    if (!string.IsNullOrEmpty(metadata.Id))
                        return;
                }
            }
            catch { }
        }

        private static string ReadNullTerminatedString(byte[] data, int offset)
        {
            int length = 0;
            while (offset + length < data.Length && data[offset + length] != 0)
                length++;

            if (length == 0) return string.Empty;

            return Encoding.UTF8.GetString(data, offset, length).Trim();
        }

        private static uint FindDirectoryInStream(Stream stream, uint dirLBA, string dirName)
        {
            stream.Seek(dirLBA * 2048, SeekOrigin.Begin);
            byte[] sector = new byte[2048];
            stream.ReadExactly(sector, 0, 2048);

            return FindDirectoryInSector(sector, dirName);
        }

        private static uint FindDirectory(ChdBlockDevice device, uint dirLBA, string dirName)
        {
            var sector = device.ReadBlock(dirLBA);
            if (sector == null) return 0;

            return FindDirectoryInSector(sector, dirName);
        }

        private static uint FindDirectoryInSector(byte[] sector, string dirName)
        {
            int pos = 0;
            while (pos < sector.Length)
            {
                byte recordLen = sector[pos];
                if (recordLen == 0) break;
                if (pos + 33 >= sector.Length) break;

                byte flags = sector[pos + 25];
                byte nameLen = sector[pos + 32];

                if (nameLen > 0 && pos + 33 + nameLen <= sector.Length)
                {
                    var name = Encoding.ASCII.GetString(sector, pos + 33, nameLen);
                    bool isDirectory = (flags & 0x02) != 0;

                    if (isDirectory && name.Equals(dirName, StringComparison.OrdinalIgnoreCase))
                        return BitConverter.ToUInt32(sector, pos + 2);
                }

                pos += recordLen;
            }

            return 0;
        }

        private static (uint lba, uint size)? FindFileInStream(Stream stream, uint dirLBA, string fileName)
        {
            stream.Seek(dirLBA * 2048, SeekOrigin.Begin);
            byte[] sector = new byte[2048];
            stream.ReadExactly(sector, 0, 2048);

            return FindFileInSector(sector, fileName);
        }

        private static (uint lba, uint size)? FindFile(ChdBlockDevice device, uint dirLBA, string fileName)
        {
            var sector = device.ReadBlock(dirLBA);
            if (sector == null) return null;

            return FindFileInSector(sector, fileName);
        }

        private static (uint lba, uint size)? FindFileInSector(byte[] sector, string fileName)
        {
            int pos = 0;
            while (pos < sector.Length)
            {
                byte recordLen = sector[pos];
                if (recordLen == 0) break;
                if (pos + 33 >= sector.Length) break;

                byte flags = sector[pos + 25];
                byte nameLen = sector[pos + 32];

                if (nameLen > 0 && pos + 33 + nameLen <= sector.Length)
                {
                    var name = Encoding.ASCII.GetString(sector, pos + 33, nameLen);
                    bool isDirectory = (flags & 0x02) != 0;

                    if (!isDirectory && name.StartsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        uint lba = BitConverter.ToUInt32(sector, pos + 2);
                        uint size = BitConverter.ToUInt32(sector, pos + 10);
                        return (lba, size);
                    }
                }

                pos += recordLen;
            }

            return null;
        }

        private static byte[] ReadFileFromStream(Stream stream, uint startLBA, uint fileSize)
        {
            uint readSize = Math.Min(fileSize, 1024 * 1024);
            byte[] result = new byte[readSize];

            stream.Seek(startLBA * 2048, SeekOrigin.Begin);
            stream.ReadExactly(result, 0, (int)readSize);

            return result;
        }

        private static byte[] ReadFile(ChdBlockDevice device, uint startLBA, uint fileSize)
        {
            uint readSize = Math.Min(fileSize, 1024 * 1024);
            byte[] result = new byte[readSize];
            uint sectorsNeeded = (readSize + 2047) / 2048;

            for (uint i = 0; i < sectorsNeeded; i++)
            {
                var sector = device.ReadBlock(startLBA + i);
                if (sector == null) break;

                uint copySize = Math.Min(2048, readSize - i * 2048);
                Array.Copy(sector, 0, result, i * 2048, copySize);
            }

            return result;
        }

        private static void FallbackFullScan(LibChdrWrapper chd, ChdrHeader header, ExtractorMetadata metadata)
        {
            try
            {
                byte[] prevHunk = null;

                for (uint i = 0; i < header.totalhunks && i < 5000; i++)
                {
                    var hunk = chd.ReadHunk(i);
                    if (hunk == null) continue;

                    if (metadata.LogoImage == null)
                    {
                        var iconData = SearchPngInHunk(hunk);
                        if (iconData != null)
                        {
                            metadata.LogoImage = iconData;
                            metadata.CoverImage = iconData;
                        }
                    }

                    if (prevHunk != null && prevHunk.Length >= 4096 && hunk.Length >= 4096)
                    {
                        byte[] boundary = new byte[8192];
                        Array.Copy(prevHunk, prevHunk.Length - 4096, boundary, 0, 4096);
                        Array.Copy(hunk, 0, boundary, 4096, 4096);

                        if (metadata.LogoImage == null)
                        {
                            var iconData = SearchPngInHunk(boundary);
                            if (iconData != null)
                            {
                                metadata.LogoImage = iconData;
                                metadata.CoverImage = iconData;
                            }
                        }
                    }

                    prevHunk = hunk;

                    if (metadata.LogoImage != null)
                        break;
                }
            }
            catch { }
        }

        private static byte[] SearchInHunk(byte[] hunk, byte b0, byte b1, byte b2, byte b3, int maxSize)
        {
            for (int pos = 0; pos < hunk.Length - 4; pos++)
            {
                if (hunk[pos] == b0 && hunk[pos + 1] == b1 &&
                    hunk[pos + 2] == b2 && hunk[pos + 3] == b3)
                {
                    int size = Math.Min(maxSize, hunk.Length - pos);
                    byte[] data = new byte[size];
                    Array.Copy(hunk, pos, data, 0, size);
                    return data;
                }
            }
            return null;
        }

        private static byte[] SearchPngInHunk(byte[] hunk)
        {
            for (int pos = 0; pos < hunk.Length - 8; pos++)
            {
                if (IsPngSignature(hunk, pos))
                {
                    int maxSize = Math.Min(1024 * 1024, hunk.Length - pos);
                    int actualSize = FindPngEnd(hunk, pos + maxSize) - pos;

                    if (actualSize > 0 && actualSize <= maxSize)
                    {
                        byte[] iconData = new byte[actualSize];
                        Array.Copy(hunk, pos, iconData, 0, actualSize);
                        return iconData;
                    }
                }
            }
            return null;
        }

        public static void ClearCache()
        {
            _cache.Clear();
        }
    }
}