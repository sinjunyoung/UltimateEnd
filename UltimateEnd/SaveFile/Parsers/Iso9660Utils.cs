using System;
using System.Text;

namespace UltimateEnd.SaveFile.Parsers
{
    public static class Iso9660Utils
    {
        public static uint FindDirectory(byte[] sector, string dirName)
        {
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

        public static (uint lba, uint size)? FindFile(byte[] sector, string fileName)
        {
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

        public static byte[] ReadFileFromSectors(Func<uint, byte[]?> readSector, uint startLBA, uint fileSize)
        {
            byte[] result = new byte[fileSize];
            uint sectorsNeeded = (fileSize + 2047) / 2048;

            for (uint i = 0; i < sectorsNeeded; i++)
            {
                var sector = readSector(startLBA + i);

                if (sector == null) break;

                uint copySize = Math.Min(2048, fileSize - i * 2048);
                Array.Copy(sector, 0, result, i * 2048, copySize);
            }

            return result;
        }

        public static byte[]? ReadPrimaryVolumeDescriptor(Func<uint, byte[]?> readSector)
        {
            var pvd = readSector(16);

            if (pvd == null || pvd.Length < 2048) return null;

            if (pvd[0] != 0x01 || pvd[1] != 0x43 || pvd[2] != 0x44 || pvd[3] != 0x30 || pvd[4] != 0x30 || pvd[5] != 0x31) return null;

            return pvd;
        }

        public static uint GetRootLBA(byte[] pvd) => BitConverter.ToUInt32(pvd, 158);
    }
}