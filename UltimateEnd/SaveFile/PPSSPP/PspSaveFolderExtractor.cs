using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UltimateEnd.Models;
using UltimateEnd.SaveFile.CHD;

namespace UltimateEnd.SaveFile
{
    public static class PspSaveFolderExtractor
    {
        public static string? ExtractSaveFolderId(string romPath)
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

        #region CHD - EBOOT.BIN 추출 및 복호화

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
                    var metadata = chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA2, 0) ?? chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA, 0);

                    if (metadata != null && metadata.Contains("TYPE:MODE1"))
                        unitbytes = 2352;
                    else
                        unitbytes = 2048;
                }

                if (unitcount == 0)
                    unitcount = header.logicalbytes / unitbytes;

                var blockDevice = new ChdBlockDevice(chd, header.hunkbytes, unitbytes, (uint)unitcount);

                var ebootData = ExtractEbootViaIso9660(blockDevice);

                if (ebootData == null) return null;

                return DecryptAndSearchSavePath(ebootData);
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ExtractEbootViaIso9660(ChdBlockDevice device)
        {
            try
            {
                var pvd = device.ReadBlock(16);

                if (pvd == null || pvd.Length < 2048) return null;

                if (pvd[0] != 0x01 || pvd[1] != 0x43 || pvd[2] != 0x44 || pvd[3] != 0x30 || pvd[4] != 0x30 || pvd[5] != 0x31) return null;

                uint rootLBA = BitConverter.ToUInt32(pvd, 158);
                uint pspGameLBA = FindDirectory(device, rootLBA, "PSP_GAME");

                if (pspGameLBA == 0) return null;

                uint sysdirLBA = FindDirectory(device, pspGameLBA, "SYSDIR");

                if (sysdirLBA == 0) return null;

                var ebootInfo = FindFile(device, sysdirLBA, "EBOOT.BIN");

                if (ebootInfo == null) return null;

                return ReadFile(device, ebootInfo.Value.lba, ebootInfo.Value.size);
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region ISO - EBOOT.BIN 추출

        private static string? ExtractFromIso(string isoPath)
        {
            try
            {
                using var stream = File.OpenRead(isoPath);
                var ebootData = FindEbootInStream(stream);

                if (ebootData != null) return DecryptAndSearchSavePath(ebootData);

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? FindEbootInStream(Stream stream)
        {
            stream.Seek(16 * 2048, SeekOrigin.Begin);
            var pvd = new byte[2048];

            if (stream.Read(pvd, 0, 2048) < 2048) return null;

            if (pvd[0] != 0x01 || pvd[1] != 0x43 || pvd[2] != 0x44) return null;

            uint rootLBA = BitConverter.ToUInt32(pvd, 158);
            var pspGameLBA = FindDirectoryInStream(stream, rootLBA, "PSP_GAME");

            if (pspGameLBA == 0) return null;

            var sysdirLBA = FindDirectoryInStream(stream, pspGameLBA, "SYSDIR");

            if (sysdirLBA == 0) return null;

            var ebootInfo = FindFileInStream(stream, sysdirLBA, "EBOOT.BIN");

            if (ebootInfo == null) return null;

            return ReadFileFromStream(stream, ebootInfo.Value.lba, ebootInfo.Value.size);
        }

        #endregion

        #region CSO - EBOOT.BIN 추출

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

                var totalBlocks = (int)((totalBytes + blockSize - 1) / blockSize);
                var indexTable = new uint[totalBlocks + 1];

                stream.Seek(headerSize, SeekOrigin.Begin);

                for (int i = 0; i <= totalBlocks; i++)
                    indexTable[i] = reader.ReadUInt32();

                using var decompressedData = new MemoryStream();

                for (int i = 0; i < totalBlocks; i++)
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
                var ebootData = FindEbootInStream(decompressedData);

                if (ebootData != null) return DecryptAndSearchSavePath(ebootData);

                return null;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region EBOOT.BIN 복호화 및 세이브 경로 검색

        private static string? DecryptAndSearchSavePath(byte[] ebootData)
        {
            byte[]? decryptedData = PspPrxDecrypter.PartialDecrypt(ebootData);

            decryptedData ??= ebootData;

            string? saveId = PspPrxDecrypter.ExtractSaveFolderId(decryptedData);

            return saveId;
        }

        #endregion

        #region ISO9660 헬퍼 함수들

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
            byte[] result = new byte[fileSize];
            uint sectorsNeeded = (fileSize + 2047) / 2048;

            for (uint i = 0; i < sectorsNeeded; i++)
            {
                var sector = device.ReadBlock(startLBA + i);

                if (sector == null) break;

                uint copySize = Math.Min(2048, fileSize - i * 2048);
                Array.Copy(sector, 0, result, i * 2048, copySize);
            }

            return result;
        }

        private static uint FindDirectoryInStream(Stream stream, uint dirLBA, string dirName)
        {
            stream.Seek(dirLBA * 2048, SeekOrigin.Begin);
            var sector = new byte[2048];

            if (stream.Read(sector, 0, 2048) < 2048) return 0;

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

        private static (uint lba, uint size)? FindFileInStream(Stream stream, uint dirLBA, string fileName)
        {
            stream.Seek(dirLBA * 2048, SeekOrigin.Begin);
            var sector = new byte[2048];

            if (stream.Read(sector, 0, 2048) < 2048) return null;

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
            byte[] result = new byte[fileSize];
            uint sectorsNeeded = (fileSize + 2047) / 2048;

            for (uint i = 0; i < sectorsNeeded; i++)
            {
                stream.Seek((startLBA + i) * 2048, SeekOrigin.Begin);
                uint copySize = Math.Min(2048, fileSize - i * 2048);
                stream.ReadExactly(result, (int)(i * 2048), (int)copySize);
            }

            return result;
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

        #region 유틸리티

        public static string? GetSaveFolderId(GameMetadata game) => ExtractSaveFolderId(game.GetRomFullPath());

        #endregion
    }
}