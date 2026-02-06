using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.SaveFile.CHD;

namespace UltimateEnd.Extractor
{
    public class Ps2MetadataExtractor : IMetadataExtractor
    {
        private static readonly ConcurrentDictionary<string, ExtractorMetadata> _cache = new();
                

        public async Task<ExtractorMetadata> Extract(string filePath)
        {
            if (_cache.TryGetValue(filePath, out var cached)) return cached;

            var ext = Path.GetExtension(filePath).ToLower();

            var metadata = ext switch
            {
                ".iso" => await ExtractFromISO(filePath),
                ".bin" => await ExtractFromBIN(filePath),
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

                    return ExtractMetadataFromStream(stream, false);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static async Task<ExtractorMetadata> ExtractFromBIN(string binPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(binPath);

                    return ExtractMetadataFromStream(stream, true);
                }
                catch
                {
                    return null;
                }
            });
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

                    if (unitbytes == 0 || unitbytes == 2448 || unitbytes == 2352 || unitbytes > 10000)
                    {
                        var metadata = chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA2, 0) ?? chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA, 0);

                        if (metadata != null)
                        {
                            if (unitbytes == 0)
                            {
                                if (metadata.Contains("TYPE:MODE1")) unitbytes = 2048;
                                else if (metadata.Contains("MODE2_RAW")) unitbytes = 2448;
                                else unitbytes = 2352;
                            }
                        }
                    }

                    if (unitcount == 0) unitcount = header.logicalbytes / unitbytes;

                    var blockDevice = new ChdBlockDevice(chd, header.hunkbytes, unitbytes, (uint)unitcount);

                    return ExtractMetadataFromBlockDevice(blockDevice);
                }
                catch
                {
                    return null;
                }
            });
        }


        private static (int sectorSize, int headerOffset) DetectBinStructure(Stream stream)
        {
            int[] possibleSizes = [2352, 2336, 2048];
            int[] possibleOffsets = [24, 16, 0];

            foreach (int size in possibleSizes)
            {
                foreach (int offset in possibleOffsets)
                {
                    long testPos = (16L * size) + offset;

                    if (testPos + 6 > stream.Length) continue;

                    stream.Seek(testPos, SeekOrigin.Begin);
                    byte[] sig = new byte[6];
                    stream.ReadExactly(sig, 0, 6);

                    if (sig[0] == 0x01 && sig[1] == 'C' && sig[2] == 'D' && sig[3] == '0' && sig[4] == '0' && sig[5] == '1') return (size, offset);
                }
            }

            return (2352, 24);
        }

        private static (uint lba, uint size)? FindFileInBinStream(Stream stream, uint dirLBA, string fileName, int sectorSize, int dataOffset)
        {
            stream.Seek(dirLBA * sectorSize + dataOffset, SeekOrigin.Begin);
            byte[] sector = new byte[2048];
            stream.ReadExactly(sector, 0, 2048);

            return FindFileInSector(sector, fileName);
        }

        private static byte[] ReadFileFromBinStream(Stream stream, uint startLBA, uint fileSize, int sectorSize, int dataOffset)
        {
            uint readSize = Math.Min(fileSize, 10240);
            byte[] result = new byte[readSize];
            uint sectorsNeeded = (readSize + 2047) / 2048;
            byte[] sectorBuffer = new byte[2048];

            for (uint i = 0; i < sectorsNeeded; i++)
            {
                stream.Seek((startLBA + i) * sectorSize + dataOffset, SeekOrigin.Begin);
                uint copySize = Math.Min(2048, readSize - i * 2048);
                stream.ReadExactly(sectorBuffer, 0, (int)copySize);
                Array.Copy(sectorBuffer, 0, result, i * 2048, copySize);
            }

            return result;
        }

        private static ExtractorMetadata ExtractMetadataFromBlockDevice(ChdBlockDevice device)
        {
            var metadata = new ExtractorMetadata();

            try
            {
                var pvd = device.ReadBlock(16);

                if (pvd != null && pvd.Length >= 2048)
                {
                    if (pvd[0] == 0x01 && pvd[1] == 0x43 && pvd[2] == 0x44 && pvd[3] == 0x30 && pvd[4] == 0x30 && pvd[5] == 0x31)
                    {
                        uint rootLBA = BitConverter.ToUInt32(pvd, 158);

                        var systemCnfInfo = FindFile(device, rootLBA, "SYSTEM.CNF");

                        if (systemCnfInfo.HasValue)
                        {
                            byte[] systemCnfData = ReadFile(device, systemCnfInfo.Value.lba, systemCnfInfo.Value.size);

                            if (systemCnfData != null)
                            {
                                string systemCnf = Encoding.ASCII.GetString(systemCnfData);
                                string bootFile = ParseSystemCnf(systemCnf);

                                if (!string.IsNullOrEmpty(bootFile))
                                {
                                    string serial = ExtractSerialFromBootFile(bootFile);
                                    metadata.Id = serial;

                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return metadata;
        }

        private static string ParseSystemCnf(string systemCnf)
        {
            try
            {
                var lines = systemCnf.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("BOOT2", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("BOOT", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmedLine.Split('=');

                        if (parts.Length >= 2)
                        {
                            var bootPath = parts[1].Trim();
                            bootPath = bootPath.Replace("cdrom0:\\", string.Empty).Replace("cdrom0:/", string.Empty).Replace("cdrom:\\", string.Empty).Replace("cdrom:/", string.Empty).Replace("\\", string.Empty).Replace("/", string.Empty);
                            var semicolonIndex = bootPath.IndexOf(';');

                            if (semicolonIndex > 0) bootPath = bootPath[..semicolonIndex];

                            return bootPath.Trim();
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static string ExtractSerialFromBootFile(string bootFile)
        {
            try
            {
                bootFile = bootFile.ToUpper().Trim();

                if (bootFile.EndsWith(".ELF")) bootFile = bootFile[..^4];

                bootFile = bootFile.Replace('_', '-');

                bootFile = bootFile.Replace(".", string.Empty);

                return bootFile;
            }
            catch
            {
                return null;
            }
        }

        private static (uint lba, uint size)? FindFile(ChdBlockDevice device, uint dirLBA, string fileName)
        {
            var sector = device.ReadBlock(dirLBA);

            if (sector == null) return null;

            return FindFileInSector(sector, fileName);
        }

        private static (uint lba, uint size)? FindFileInStream(Stream stream, uint dirLBA, string fileName)
        {
            stream.Seek(dirLBA * 2048, SeekOrigin.Begin);
            byte[] sector = new byte[2048];
            stream.ReadExactly(sector, 0, 2048);

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
                    var cleanName = name;
                    var semicolonIdx = name.IndexOf(';');

                    if (semicolonIdx > 0) cleanName = name[..semicolonIdx];

                    if (!isDirectory && cleanName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
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

        private static byte[] ReadFileFromStream(Stream stream, uint startLBA, uint fileSize)
        {
            uint readSize = Math.Min(fileSize, 10240);
            byte[] result = new byte[readSize];

            stream.Seek(startLBA * 2048, SeekOrigin.Begin);
            stream.ReadExactly(result, 0, (int)readSize);

            return result;
        }

        public static void ClearCache() => _cache.Clear();

        private static ExtractorMetadata ExtractMetadataFromStream(Stream stream, bool isBin)
        {
            var metadata = new ExtractorMetadata();
            try
            {
                int sectorSize = 2048;
                int headerOffset = 0;

                if (isBin)
                    (sectorSize, headerOffset) = DetectBinStructure(stream);

                long pvdOffset = (16L * sectorSize) + headerOffset;
                if (pvdOffset + 2048 > stream.Length) return metadata;

                stream.Seek(pvdOffset, SeekOrigin.Begin);
                byte[] pvd = new byte[2048];
                stream.ReadExactly(pvd, 0, 2048);

                if (pvd[0] != 0x01 || pvd[1] != 'C' || pvd[2] != 'D' || pvd[3] != '0' || pvd[4] != '0' || pvd[5] != '1')
                    return metadata;

                uint rootLBA = BitConverter.ToUInt32(pvd, 158);

                (uint lba, uint size)? systemCnfInfo = isBin
                    ? FindFileInBinStream(stream, rootLBA, "SYSTEM.CNF", sectorSize, headerOffset)
                    : FindFileInStream(stream, rootLBA, "SYSTEM.CNF");

                if (!systemCnfInfo.HasValue) return metadata;

                byte[] systemCnfData = isBin
                    ? ReadFileFromBinStream(stream, systemCnfInfo.Value.lba, systemCnfInfo.Value.size, sectorSize, headerOffset)
                    : ReadFileFromStream(stream, systemCnfInfo.Value.lba, systemCnfInfo.Value.size);

                if (systemCnfData == null) return metadata;

                string systemCnf = Encoding.ASCII.GetString(systemCnfData);
                string bootFile = ParseSystemCnf(systemCnf);

                if (!string.IsNullOrEmpty(bootFile))
                {
                    string serial = ExtractSerialFromBootFile(bootFile);
                    metadata.Id = serial;
                }
            }
            catch { }

            return metadata;
        }
    }
}