using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UltimateEnd.SaveFile.CHD;
using UltimateEnd.SaveFile.Parsers;

namespace UltimateEnd.Extractor
{
    public class Ps2MetadataExtractor : IMetadataExtractor
    {
        private static readonly ConcurrentDictionary<string, ExtractedMetadata> _cache = new();
        private static Dictionary<string, GameIndexEntry> _gameIndex;
        private static readonly object _indexLock = new object();

        public async Task<ExtractedMetadata> Extract(string filePath)
        {
            if (_cache.TryGetValue(filePath, out var cached)) return cached;

            var ext = Path.GetExtension(filePath).ToLower();
            var metadata = ext switch
            {
                ".iso" => await ExtractFromISO(filePath),
                ".chd" => await ExtractFromCHD(filePath),
                _ => null,
            };

            if (metadata != null) _cache[filePath] = metadata;

            return metadata;
        }

        private static void LoadGameIndex(string yamlPath)
        {
            if (_gameIndex != null) return;

            lock (_indexLock)
            {
                if (_gameIndex != null) return;

                try
                {
                    _gameIndex = new Dictionary<string, GameIndexEntry>(StringComparer.OrdinalIgnoreCase);

                    var lines = File.ReadAllLines(yamlPath);
                    string currentSerial = null;
                    GameIndexEntry currentEntry = null;

                    foreach (var line in lines)
                    {
                        // 주석이나 빈 줄 무시
                        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                            continue;

                        // Serial 라인 (들여쓰기 없이 시작하고 : 로 끝남)
                        if (!line.StartsWith(" ") && line.Contains(":"))
                        {
                            var colonIndex = line.IndexOf(':');
                            currentSerial = line.Substring(0, colonIndex).Trim();
                            currentEntry = new GameIndexEntry();
                            _gameIndex[currentSerial] = currentEntry;
                        }
                        // 속성 라인 (들여쓰기로 시작)
                        else if (line.StartsWith(" ") && currentEntry != null)
                        {
                            var trimmed = line.Trim();

                            if (trimmed.StartsWith("name-en:"))
                            {
                                var value = ExtractYamlValue(trimmed, "name-en:");
                                if (!string.IsNullOrEmpty(value))
                                    currentEntry.NameEn = value;
                            }
                            else if (trimmed.StartsWith("name:") && !trimmed.StartsWith("name-sort:"))
                            {
                                var value = ExtractYamlValue(trimmed, "name:");
                                if (!string.IsNullOrEmpty(value))
                                    currentEntry.Name = value;
                            }
                            else if (trimmed.StartsWith("region:"))
                            {
                                var value = ExtractYamlValue(trimmed, "region:");
                                if (!string.IsNullOrEmpty(value))
                                    currentEntry.Region = value;
                            }
                        }
                    }
                }
                catch
                {
                    _gameIndex = new Dictionary<string, GameIndexEntry>();
                }
            }
        }

        private static string ExtractYamlValue(string line, string prefix)
        {
            try
            {
                var value = line.Substring(prefix.Length).Trim();

                // 따옴표 제거
                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                return value;
            }
            catch
            {
                return null;
            }
        }

        private static string GetTitleFromSerial(string serial, string yamlPath)
        {
            LoadGameIndex(yamlPath);

            Debug.Write($"{serial}");

            if (_gameIndex != null && _gameIndex.TryGetValue(serial, out var entry))
            {
                Debug.WriteLine($"/{entry.Name}");

                return !string.IsNullOrEmpty(entry.Name) ? entry.Name : entry.NameEn;
            }

            Debug.WriteLine($"/");

            return null;
        }

        private static async Task<ExtractedMetadata> ExtractFromISO(string isoPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(isoPath);
                    return ExtractMetadataFromStream(stream, isoPath);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static async Task<ExtractedMetadata> ExtractFromCHD(string chdPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var chd = new LibChdrWrapper();

                    if (chd.Open(chdPath) != ChdrError.CHDERR_NONE || !chd.Header.HasValue)
                        return null;

                    var header = chd.Header.Value;
                    uint unitbytes = header.unitbytes;
                    ulong unitcount = header.unitcount;

                    // CD-ROM 모드 감지
                    if (unitbytes == 0 || unitbytes > 10000)
                    {
                        var metadata = chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA2, 0) ??
                                      chd.GetMetadata(LibChdr.CDROM_TRACK_METADATA, 0);

                        if (metadata != null && metadata.Contains("TYPE:MODE1"))
                            unitbytes = 2352;
                        else
                            unitbytes = 2048;
                    }

                    if (unitcount == 0)
                        unitcount = header.logicalbytes / unitbytes;

                    var blockDevice = new ChdBlockDevice(chd, header.hunkbytes, unitbytes, (uint)unitcount);
                    return ExtractMetadataFromBlockDevice(blockDevice, chdPath);
                }
                catch
                {
                    return null;
                }
            });
        }

        private static ExtractedMetadata ExtractMetadataFromStream(Stream stream, string filePath)
        {
            var metadata = new ExtractedMetadata();

            try
            {
                // ISO9660 파싱 시도
                if (TryParseIso9660Stream(stream, metadata, filePath))
                    return metadata;
            }
            catch { }

            if (string.IsNullOrEmpty(metadata.Title))
                metadata.Title = "Unknown Title";

            return metadata;
        }

        private static bool TryParseIso9660Stream(Stream stream, ExtractedMetadata metadata, string filePath)
        {
            try
            {
                // Primary Volume Descriptor는 섹터 16에 위치
                stream.Seek(16 * 2048, SeekOrigin.Begin);
                byte[] pvd = new byte[2048];
                stream.ReadExactly(pvd, 0, 2048);

                // ISO9660 시그니처 확인: 0x01 'C' 'D' '0' '0' '1'
                if (pvd[0] != 0x01 || pvd[1] != 0x43 || pvd[2] != 0x44 ||
                    pvd[3] != 0x30 || pvd[4] != 0x30 || pvd[5] != 0x31)
                    return false;

                // Root directory LBA
                uint rootLBA = BitConverter.ToUInt32(pvd, 158);

                // SYSTEM.CNF 파일 찾기
                var systemCnfInfo = FindFileInStream(stream, rootLBA, "SYSTEM.CNF");
                if (!systemCnfInfo.HasValue)
                    return false;

                // SYSTEM.CNF 읽기
                byte[] systemCnfData = ReadFileFromStream(stream, systemCnfInfo.Value.lba, systemCnfInfo.Value.size);
                if (systemCnfData == null)
                    return false;

                string systemCnf = Encoding.ASCII.GetString(systemCnfData);
                string bootFile = ParseSystemCnf(systemCnf);

                if (string.IsNullOrEmpty(bootFile))
                    return false;

                // Serial 추출
                string serial = ExtractSerialFromBootFile(bootFile);
                if (!string.IsNullOrEmpty(serial))
                {
                    // GameIndex.yaml 경로 찾기
                    string yamlPath = FindGameIndexYaml(filePath);
                    if (!string.IsNullOrEmpty(yamlPath))
                    {
                        string title = GetTitleFromSerial(serial, yamlPath);
                        if (!string.IsNullOrEmpty(title))
                        {
                            metadata.Title = title;
                            return true;
                        }
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static ExtractedMetadata ExtractMetadataFromBlockDevice(ChdBlockDevice device, string filePath)
        {
            var metadata = new ExtractedMetadata();

            try
            {
                // Primary Volume Descriptor 읽기
                var pvd = device.ReadBlock(16);
                if (pvd != null && pvd.Length >= 2048)
                {
                    if (pvd[0] == 0x01 && pvd[1] == 0x43 && pvd[2] == 0x44 &&
                        pvd[3] == 0x30 && pvd[4] == 0x30 && pvd[5] == 0x31)
                    {
                        uint rootLBA = BitConverter.ToUInt32(pvd, 158);

                        // SYSTEM.CNF 찾기
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
                                    // Serial 추출
                                    string serial = ExtractSerialFromBootFile(bootFile);
                                    if (!string.IsNullOrEmpty(serial))
                                    {
                                        // GameIndex.yaml 경로 찾기
                                        string yamlPath = FindGameIndexYaml(filePath);
                                        if (!string.IsNullOrEmpty(yamlPath))
                                        {
                                            string title = GetTitleFromSerial(serial, yamlPath);
                                            if (!string.IsNullOrEmpty(title))
                                                metadata.Title = title;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(metadata.Title))
                metadata.Title = "Unknown Title";

            return metadata;
        }

        private static string ParseSystemCnf(string systemCnf)
        {
            try
            {
                // BOOT2 = cdrom0:\SLUS_123.45;1 형식
                var lines = systemCnf.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("BOOT2", StringComparison.OrdinalIgnoreCase) ||
                        trimmedLine.StartsWith("BOOT", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = trimmedLine.Split('=');
                        if (parts.Length >= 2)
                        {
                            var bootPath = parts[1].Trim();

                            // cdrom0:\SLUS_123.45;1 -> SLUS_123.45
                            bootPath = bootPath.Replace("cdrom0:\\", "")
                                             .Replace("cdrom0:/", "")
                                             .Replace("cdrom:\\", "")
                                             .Replace("cdrom:/", "")
                                             .Replace("\\", "")
                                             .Replace("/", "");

                            // ;1 제거
                            var semicolonIndex = bootPath.IndexOf(';');
                            if (semicolonIndex > 0)
                                bootPath = bootPath.Substring(0, semicolonIndex);

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
                // SLUS_123.45 -> SLUS-12345
                // SCPS_123.45 -> SCPS-12345

                bootFile = bootFile.ToUpper().Trim();

                // .ELF 확장자 제거
                if (bootFile.EndsWith(".ELF"))
                    bootFile = bootFile.Substring(0, bootFile.Length - 4);

                // 언더스코어를 하이픈으로 변경
                bootFile = bootFile.Replace('_', '-');

                // 점(.) 제거
                bootFile = bootFile.Replace(".", "");

                return bootFile;
            }
            catch
            {
                return null;
            }
        }

        private static string FindGameIndexYaml(string filePath)
        {
            try
            {
                // 파일이 있는 디렉토리에서 GameIndex.yaml 찾기
                var directory = Path.GetDirectoryName(filePath);

                // 현재 디렉토리
                var yamlPath = Path.Combine(directory, "GameIndex.yaml");
                if (File.Exists(yamlPath))
                    return yamlPath;

                // 부모 디렉토리
                var parentDir = Directory.GetParent(directory)?.FullName;
                if (parentDir != null)
                {
                    yamlPath = Path.Combine(parentDir, "GameIndex.yaml");
                    if (File.Exists(yamlPath))
                        return yamlPath;
                }

                // 고정 경로 시도
                yamlPath = @"C:\GameIndex.yaml";
                if (File.Exists(yamlPath))
                    return yamlPath;

                yamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameIndex.yaml");
                if (File.Exists(yamlPath))
                    return yamlPath;
            }
            catch { }

            return null;
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

                    // PS2 파일은 ;1 같은 버전 정보가 붙을 수 있음
                    var cleanName = name;
                    var semicolonIdx = name.IndexOf(';');
                    if (semicolonIdx > 0)
                        cleanName = name.Substring(0, semicolonIdx);

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

        public static void ClearCache()
        {
            _cache.Clear();
        }

        private class GameIndexEntry
        {
            public string Name { get; set; }
            public string NameEn { get; set; }
            public string Region { get; set; }
        }
    }
}