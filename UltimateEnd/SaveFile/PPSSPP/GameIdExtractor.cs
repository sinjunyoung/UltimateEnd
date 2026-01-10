using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UltimateEnd.Models;
using UltimateEnd.SaveFile.CHD;

namespace UltimateEnd.SaveFile.PPSSPP
{
    public static class GameIdExtractor
    {
        public static string? ExtractGameId(string romPath)
        {
            if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath)) return null;

            var extension = Path.GetExtension(romPath).ToLower();

            return extension switch
            {
                ".iso" => ExtractFromIso(romPath),
                ".cso" => ExtractFromCso(romPath),
                ".chd" => ExtractFromChd(romPath),
                _ => null
            };
        }

        #region CHD 파싱 (PPSSPP 방식)

        private static string? ExtractFromChd(string chdPath)
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
                    var metadata = chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA2, 0)
                                ?? chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA, 0);

                    if (metadata != null && metadata.Contains("TYPE:MODE1"))
                        unitbytes = 2352;
                    else
                        unitbytes = 2048;
                }

                if (unitcount == 0)
                    unitcount = header.logicalbytes / unitbytes;

                var blockDevice = new ChdBlockDevice(chd, header.hunkbytes, unitbytes, (uint)unitcount);

                var result = ParseIso9660(blockDevice);

                if (result != null) return result;

                return FallbackFullScan(chdPath);
            }
            catch
            {
                return null;
            }
        }

        private static string? ParseIso9660(ChdBlockDevice device)
        {
            try
            {
                var pvd = device.ReadBlock(16);

                if (pvd == null || pvd.Length < 2048) return null;

                if (pvd[0] != 0x01 || pvd[1] != 0x43 || pvd[2] != 0x44 || pvd[3] != 0x30 || pvd[4] != 0x30 || pvd[5] != 0x31) return null;

                uint rootLBA = BitConverter.ToUInt32(pvd, 158);
                uint pspGameLBA = FindDirectory(device, rootLBA, "PSP_GAME");

                if (pspGameLBA == 0) return null;

                var paramSfoInfo = FindFile(device, pspGameLBA, "PARAM.SFO");

                if (paramSfoInfo == null) return null;

                byte[] sfoData = ReadFile(device, paramSfoInfo.Value.lba, paramSfoInfo.Value.size);

                if (sfoData == null) return null;

                return ParseDiscIdFromParamSfo(sfoData);
            }
            catch
            {
                return null;
            }
        }

        private static uint FindDirectory(ChdBlockDevice device, uint dirLBA, string dirName)
        {
            var sector = device.ReadBlock(dirLBA);

            if (sector == null) return 0;

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

                    if (isDirectory && name.Equals(dirName, StringComparison.OrdinalIgnoreCase)) return BitConverter.ToUInt32(sector, pos + 2);
                }

                pos += recordLen;
            }

            return 0;
        }

        private static (uint lba, uint size)? FindFile(ChdBlockDevice device, uint dirLBA, string fileName)
        {
            var sector = device.ReadBlock(dirLBA);

            if (sector == null) return null;

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

        private static byte[] ReadFile(ChdBlockDevice device, uint startLBA, uint fileSize)
        {
            uint readSize = Math.Min(fileSize, 10240);
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

        private static string? FallbackFullScan(string chdPath)
        {
            try
            {
                using var chd = new LibChdrWrapper();

                if (chd.Open(chdPath) != ChdrError.CHDERR_NONE || !chd.Header.HasValue) return null;

                var header = chd.Header.Value;
                byte[] sfoMagic = [0x00, 0x50, 0x53, 0x46];
                byte[]? prevHunk = null;

                for (uint i = 0; i < header.totalhunks; i++)
                {
                    var hunk = chd.ReadHunk(i);

                    if (hunk == null) continue;

                    var result = SearchHunkForSfo(hunk, sfoMagic);

                    if (result != null) return result;
                    
                    if (prevHunk != null && prevHunk.Length >= 4096 && hunk.Length >= 4096)
                    {
                        byte[] boundary = new byte[8192];
                        Array.Copy(prevHunk, prevHunk.Length - 4096, boundary, 0, 4096);
                        Array.Copy(hunk, 0, boundary, 4096, 4096);

                        result = SearchHunkForSfo(boundary, sfoMagic);

                        if (result != null) return result;
                    }

                    prevHunk = hunk;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string? SearchHunkForSfo(byte[] hunk, byte[] sfoMagic)
        {
            for (int pos = 0; pos < hunk.Length - 4; pos++)
            {
                if (hunk[pos] == sfoMagic[0] && hunk[pos + 1] == sfoMagic[1] && hunk[pos + 2] == sfoMagic[2] && hunk[pos + 3] == sfoMagic[3])
                {
                    int sfoSize = Math.Min(20480, hunk.Length - pos);
                    byte[] sfoData = new byte[sfoSize];
                    Array.Copy(hunk, pos, sfoData, 0, sfoSize);

                    var discId = ParseDiscIdFromParamSfo(sfoData);

                    if (!string.IsNullOrEmpty(discId)) return discId;
                }
            }
            return null;
        }

        #endregion

        #region ISO 파싱

        private static string? ExtractFromIso(string isoPath)
        {
            try
            {
                using var stream = File.OpenRead(isoPath);
                var paramSfoData = FindParamSfoInStream(stream);

                if (paramSfoData != null) return ParseDiscIdFromParamSfo(paramSfoData);

                return null;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region CSO 파싱

        private static string? ExtractFromCso(string csoPath)
        {
            try
            {
                using var stream = File.OpenRead(csoPath);
                using var reader = new BinaryReader(stream);

                var magic = reader.ReadBytes(4);

                if (Encoding.ASCII.GetString(magic) != "CISO") return null;

                var headerSize = reader.ReadUInt32();
                var totalBytes = reader.ReadUInt64();
                var blockSize = reader.ReadUInt32();
                reader.ReadByte();
                reader.ReadByte();

                var totalBlocks = (int)((totalBytes + blockSize - 1) / blockSize);
                var indexTable = new uint[totalBlocks + 1];

                stream.Seek(headerSize, SeekOrigin.Begin);

                for (int i = 0; i <= totalBlocks; i++)
                    indexTable[i] = reader.ReadUInt32();

                var searchBlocks = Math.Min(100, totalBlocks);
                using var decompressedData = new MemoryStream();

                for (int i = 0; i < searchBlocks; i++)
                {
                    var blockOffset = indexTable[i] & 0x7FFFFFFF;
                    var nextBlockOffset = indexTable[i + 1] & 0x7FFFFFFF;
                    var isCompressed = (indexTable[i] & 0x80000000) == 0;
                    var blockDataSize = (int)(nextBlockOffset - blockOffset);

                    if (blockDataSize <= 0 || blockDataSize > blockSize * 2) continue;

                    stream.Seek(blockOffset, SeekOrigin.Begin);
                    var blockData = reader.ReadBytes(blockDataSize);

                    if (isCompressed)
                    {
                        var decompressed = DecompressZlib(blockData);
                        decompressedData.Write(decompressed, 0, decompressed.Length);
                    }
                    else
                        decompressedData.Write(blockData, 0, blockData.Length);
                }

                decompressedData.Seek(0, SeekOrigin.Begin);
                var paramSfoData = FindParamSfoInStream(decompressedData);

                if (paramSfoData != null) return ParseDiscIdFromParamSfo(paramSfoData);

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] DecompressZlib(byte[] compressedData)
        {
            using var input = new MemoryStream(compressedData, 2, compressedData.Length - 2);
            using var output = new MemoryStream();
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);

            deflate.CopyTo(output);

            return output.ToArray();
        }

        #endregion

        #region PARAM.SFO 파싱

        private static byte[]? FindParamSfoInStream(Stream stream)
        {
            var magic = new byte[] { 0x00, 0x50, 0x53, 0x46 };
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
                    if (buffer[i] == magic[0] && buffer[i + 1] == magic[1] && buffer[i + 2] == magic[2] && buffer[i + 3] == magic[3])
                    {
                        stream.Seek(offset + i, SeekOrigin.Begin);
                        var sfoData = new byte[10240];
                        var sfoSize = stream.Read(sfoData, 0, sfoData.Length);

                        if (sfoSize < sfoData.Length)
                            Array.Resize(ref sfoData, sfoSize);

                        return sfoData;
                    }
                }
            }

            return null;
        }

        private static string? ParseDiscIdFromParamSfo(byte[] sfoData)
        {
            try
            {
                if (sfoData.Length < 20) return null;

                if (sfoData[0] != 0x00 || sfoData[1] != 0x50 || sfoData[2] != 0x53 || sfoData[3] != 0x46) return null;

                var keyTableOffset = BitConverter.ToInt32(sfoData, 0x08);
                var dataTableOffset = BitConverter.ToInt32(sfoData, 0x0C);
                var entryCount = BitConverter.ToInt32(sfoData, 0x10);

                if (entryCount <= 0 || entryCount > 100) return null;

                for (int i = 0; i < entryCount; i++)
                {
                    var entryOffset = 0x14 + i * 16;

                    if (entryOffset + 16 > sfoData.Length) break;

                    var keyOffset = BitConverter.ToInt16(sfoData, entryOffset + 0);
                    var dataOffset = BitConverter.ToInt32(sfoData, entryOffset + 12);

                    if (keyTableOffset + keyOffset >= sfoData.Length || dataTableOffset + dataOffset >= sfoData.Length) continue;

                    var keyNameOffset = keyTableOffset + keyOffset;
                    var keyName = ReadNullTerminatedString(sfoData, keyNameOffset);

                    if (keyName == "DISC_ID")
                    {
                        var valueOffset = dataTableOffset + dataOffset;
                        var discId = ReadNullTerminatedString(sfoData, valueOffset);

                        if (!string.IsNullOrEmpty(discId) && IsValidGameId(discId)) return discId.ToUpper();
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static string ReadNullTerminatedString(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length) return string.Empty;

            var endIndex = offset;

            while (endIndex < data.Length && data[endIndex] != 0)
                endIndex++;

            var length = endIndex - offset;

            if (length == 0) return string.Empty;

            return Encoding.UTF8.GetString(data, offset, length);
        }

        #endregion

        #region 유틸리티

        public static bool IsValidGameId(string? id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != 9) return false;

            var prefix = id[..4].ToUpper();
            var number = id[4..];

            var validPrefixes = new[]
            {
                "ULUS", "UCUS", "NPUZ", "NPUX", "NPUF", "NPUH", "NPUG",
                "ULES", "UCES", "NPEZ", "NPEX", "NPEH", "NPEG",
                "ULJS", "ULJM", "UCJS", "UCJM", "UCJB", "NPJJ", "NPJH", "NPJG",
                "ULKS", "UCKS", "NPHH", "NPHG",
                "ULAS", "UCAS", "NPHZ"
            };

            return validPrefixes.Contains(prefix) && number.All(char.IsDigit);
        }

        public static string? GetGameId(GameMetadata game) => ExtractGameId(game.GetRomFullPath());

        #endregion
    }
}