using System;
using System.Text;
using UltimateEnd.SaveFile.CHD;

namespace UltimateEnd.SaveFile.Parsers
{
    public class PspChdParser : IFormatParser
    {
        public bool CanParse(string extension) => extension.Equals(".chd", StringComparison.InvariantCultureIgnoreCase);

        public string? ParseGameId(string filePath)
        {
            try
            {
                using var chd = new LibChdrWrapper();

                if (chd.Open(filePath) != ChdrError.CHDERR_NONE || !chd.Header.HasValue) return null;

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

                if (unitcount == 0) unitcount = header.logicalbytes / unitbytes;

                var blockDevice = new ChdBlockDevice(chd, header.hunkbytes, unitbytes, (uint)unitcount);
                var result = ParseIso9660(blockDevice);

                if (result != null) return result;

                return FallbackFullScan(chd, header);
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

                return ParamSfoParser.ParseDiscId(sfoData);
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

        private static string? FallbackFullScan(LibChdrWrapper chd, ChdrHeader header)
        {
            try
            {
                byte[]? prevHunk = null;

                for (uint i = 0; i < header.totalhunks; i++)
                {
                    var hunk = chd.ReadHunk(i);

                    if (hunk == null) continue;

                    var result = ParamSfoParser.SearchInHunk(hunk);

                    if (result != null) return result;

                    if (prevHunk != null && prevHunk.Length >= 4096 && hunk.Length >= 4096)
                    {
                        byte[] boundary = new byte[8192];
                        Array.Copy(prevHunk, prevHunk.Length - 4096, boundary, 0, 4096);
                        Array.Copy(hunk, 0, boundary, 4096, 4096);

                        result = ParamSfoParser.SearchInHunk(boundary);

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
    }
}