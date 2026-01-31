using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UltimateEnd.SaveFile.Parsers
{
    public class PspCsoParser : IFormatParser
    {
        public bool CanParse(string extension) => extension.Equals(".cso", StringComparison.CurrentCultureIgnoreCase);

        public string? ParseGameId(string filePath)
        {
            try
            {
                using var csoReader = new CsoStreamReader(filePath);

                var pvd = csoReader.ReadSector(16);

                if (pvd == null || pvd.Length < 2048) return null;

                if (pvd[0] != 0x01 || pvd[1] != 0x43 || pvd[2] != 0x44) return null;

                uint rootLBA = BitConverter.ToUInt32(pvd, 158);
                uint pspGameLBA = FindDirectory(csoReader, rootLBA, "PSP_GAME");

                if (pspGameLBA == 0) return null;

                var paramSfoInfo = FindFile(csoReader, pspGameLBA, "PARAM.SFO");

                if (paramSfoInfo == null) return null;

                byte[] sfoData = ReadFile(csoReader, paramSfoInfo.Value.lba, paramSfoInfo.Value.size);

                if (sfoData == null) return null;

                return ParamSfoParser.ParseDiscId(sfoData);
            }
            catch
            {
                return null;
            }
        }

        private static uint FindDirectory(CsoStreamReader reader, uint dirLBA, string dirName)
        {
            var sector = reader.ReadSector(dirLBA);

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
                    var name = System.Text.Encoding.ASCII.GetString(sector, pos + 33, nameLen);
                    bool isDirectory = (flags & 0x02) != 0;

                    if (isDirectory && name.Equals(dirName, StringComparison.OrdinalIgnoreCase)) return BitConverter.ToUInt32(sector, pos + 2);
                }

                pos += recordLen;
            }

            return 0;
        }

        private static (uint lba, uint size)? FindFile(CsoStreamReader reader, uint dirLBA, string fileName)
        {
            var sector = reader.ReadSector(dirLBA);

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
                    var name = System.Text.Encoding.ASCII.GetString(sector, pos + 33, nameLen);
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

        private static byte[] ReadFile(CsoStreamReader reader, uint startLBA, uint fileSize)
        {
            uint readSize = Math.Min(fileSize, 10240);
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
    }
}