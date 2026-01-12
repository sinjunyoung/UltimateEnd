using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ZstdSharp;

namespace UltimateEnd.SaveFile.Cemu
{
    public static class CemuTitleIdExtractor
    {
        public static string? ExtractTitleId(string romPath)
        {
            if (!File.Exists(romPath)) return null;

            string ext = Path.GetExtension(romPath).ToLower();

            return ext switch
            {   
                ".wua" => ExtractFromWua(romPath),
                _ => throw new NotSupportedException($"{ext.ToUpperInvariant()}는 지원하지 않습니다.\r\n.WUA만 지원합니다.")
            };
        }

        private static string? ParseTitleIdFromMetaXml(string xmlContent)
        {
            var match = Regex.Match(xmlContent, @"<title_id[^>]*>([0-9A-Fa-f]{16})</title_id>");

            if (match.Success && match.Groups[1].Value.Length == 16) return match.Groups[1].Value.Substring(8, 8);

            return null;
        }

        #region WUA

        private static string? ExtractFromWua(string wuaPath)
        {
            try
            {
                using var stream = File.OpenRead(wuaPath);
                long fileSize = stream.Length;

                if (fileSize <= 0x90) return null;

                stream.Seek(fileSize - 0x90, SeekOrigin.Begin);
                byte[] footerData = new byte[0x90];

                if (stream.Read(footerData, 0, 0x90) != 0x90) return null;

                var footer = ParseZArchiveFooter(footerData);

                if (footer == null) return null;

                var nameTable = ReadSection(stream, footer.Value.sectionNames);

                if (nameTable == null) return null;

                var fileTree = ReadFileTree(stream, footer.Value.sectionFileTree);

                if (fileTree == null || fileTree.Length <= 1) return null;

                if (!fileTree[1].isFile)
                {
                    var gameDirName = GetName(nameTable, fileTree[1].nameOffset);

                    if (gameDirName != null)
                    {
                        var dirMatch = Regex.Match(gameDirName, @"^[0-9A-Fa-f]{8}([0-9A-Fa-f]{8})");

                        if (dirMatch.Success) return dirMatch.Groups[1].Value;

                        var metaXmlNode = FindFile(fileTree, nameTable, $"{gameDirName}/meta/meta.xml");

                        if (metaXmlNode.HasValue)
                        {
                            var metaXmlContent = ReadFileFromWua(stream, fileTree[metaXmlNode.Value], footer.Value);

                            if (metaXmlContent != null)
                            {
                                string xmlContent = Encoding.UTF8.GetString(metaXmlContent);
                                return ParseTitleIdFromMetaXml(xmlContent);
                            }
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static ZArchiveFooter? ParseZArchiveFooter(byte[] data)
        {
            const uint MAGIC = 0x169f52d6;
            const uint VERSION1 = 0x61bf3a01;

            if (data.Length < 0x90) return null;

            uint magic = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(data.Length - 4, 4));
            uint version = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(data.Length - 8, 4));
            ulong totalSize = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(data.Length - 16, 8));

            if (magic != MAGIC || version != VERSION1) return null;

            int offset = 0;

            return new ZArchiveFooter
            {
                magic = magic,
                version = version,
                totalSize = totalSize,
                sectionCompressedData = ReadSectionInfoBE(data, ref offset),
                sectionOffsetRecords = ReadSectionInfoBE(data, ref offset),
                sectionNames = ReadSectionInfoBE(data, ref offset),
                sectionFileTree = ReadSectionInfoBE(data, ref offset),
                sectionMetaDirectory = ReadSectionInfoBE(data, ref offset),
                sectionMetaData = ReadSectionInfoBE(data, ref offset)
            };
        }

        private static SectionInfo ReadSectionInfoBE(byte[] data, ref int offset)
        {
            var info = new SectionInfo
            {
                offset = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset, 8)),
                size = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(offset + 8, 8))
            };
            offset += 16;

            return info;
        }

        private static byte[]? ReadSection(FileStream stream, SectionInfo section)
        {
            if (section.size > int.MaxValue) return null;

            stream.Seek((long)section.offset, SeekOrigin.Begin);
            byte[] data = new byte[section.size];

            return stream.Read(data, 0, (int)section.size) == (int)section.size ? data : null;
        }

        private static FileEntry[]? ReadFileTree(FileStream stream, SectionInfo section)
        {
            var data = ReadSection(stream, section);

            if (data == null) return null;

            const int entrySize = 16;

            if (data.Length % entrySize != 0) return null;

            int entryCount = data.Length / entrySize;
            var entries = new FileEntry[entryCount];

            for (int i = 0; i < entryCount; i++)
            {
                int offset = i * entrySize;

                if (offset + 16 > data.Length) break;

                uint flags = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4));
                uint value1 = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 4, 4));
                uint value2 = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8, 4));
                uint value3 = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 12, 4));

                bool isFile = (flags & 0x80000000) != 0;

                entries[i] = new FileEntry
                {
                    nameOffset = flags & 0x7FFFFFFF,
                    isFile = isFile,
                    offsetOrNodeStart = isFile ? value1 | ((ulong)(value3 & 0xFFFF) << 32) : value1,
                    sizeOrCount = isFile ? value2 | ((ulong)(value3 >> 16) << 32) : value2
                };
            }

            return entries;
        }

        private static string? GetName(byte[] nameTable, uint nameOffset)
        {
            if (nameOffset >= nameTable.Length) return null;

            int offset = (int)nameOffset;
            ushort nameLength = (ushort)(nameTable[offset] & 0x7F);

            if ((nameTable[offset] & 0x80) != 0)
            {
                if (offset + 1 >= nameTable.Length) return null;

                nameLength = (ushort)(nameLength | ((ushort)nameTable[offset + 1] << 7));
                offset += 2;
            }
            else
            {
                offset++;
            }

            return offset + nameLength <= nameTable.Length ? Encoding.UTF8.GetString(nameTable, offset, nameLength) : null;
        }

        private static int? FindFile(FileEntry[] fileTree, byte[] nameTable, string path)
        {
            string[] parts = path.Split('/');
            int currentNode = 0;

            foreach (var part in parts)
            {
                if (fileTree[currentNode].isFile) return null;

                ulong startIndex = fileTree[currentNode].offsetOrNodeStart;
                ulong count = fileTree[currentNode].sizeOrCount;
                bool found = false;

                for (ulong i = 0; i < count; i++)
                {
                    int nodeIndex = (int)(startIndex + i);

                    if (nodeIndex >= fileTree.Length) return null;

                    var name = GetName(nameTable, fileTree[nodeIndex].nameOffset);

                    if (name != null && name.Equals(part, StringComparison.OrdinalIgnoreCase))
                    {
                        currentNode = nodeIndex;
                        found = true;
                        break;
                    }
                }

                if (!found) return null;
            }

            return fileTree[currentNode].isFile ? currentNode : null;
        }

        private static byte[]? ReadFileFromWua(FileStream stream, FileEntry file, ZArchiveFooter footer)
        {
            if (!file.isFile || file.sizeOrCount > 1024 * 1024) return null;

            const int BLOCK_SIZE = 0x10000;
            const int ENTRIES_PER_RECORD = 16;

            ulong fileOffset = file.offsetOrNodeStart;
            ulong fileSize = file.sizeOrCount;

            try
            {
                var offsetRecords = ReadSection(stream, footer.sectionOffsetRecords);

                if (offsetRecords == null) return null;

                int recordSize = 8 + (2 * ENTRIES_PER_RECORD);
                ulong startBlockIndex = fileOffset / BLOCK_SIZE;
                ulong endBlockIndex = (fileOffset + fileSize - 1) / BLOCK_SIZE;
                using var decompressor = new Decompressor();
                var resultData = new System.Collections.Generic.List<byte>();

                for (ulong blockIdx = startBlockIndex; blockIdx <= endBlockIndex; blockIdx++)
                {
                    var blockData = ReadBlockData(stream, footer, offsetRecords, recordSize, blockIdx, decompressor);

                    if (blockData == null) return null;

                    resultData.AddRange(blockData);
                }

                int offsetInDecompressed = (int)(fileOffset - (startBlockIndex * BLOCK_SIZE));

                if ((ulong)offsetInDecompressed + fileSize > (ulong)resultData.Count) return null;

                return [.. resultData.GetRange(offsetInDecompressed, (int)fileSize)];
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? ReadBlockData(FileStream stream, ZArchiveFooter footer, byte[] offsetRecords, int recordSize, ulong blockIndex, Decompressor decompressor)
        {
            const int BLOCK_SIZE = 0x10000;
            const int ENTRIES_PER_RECORD = 16;

            try
            {
                int recordIndex = (int)(blockIndex / ENTRIES_PER_RECORD);
                int subIndex = (int)(blockIndex % ENTRIES_PER_RECORD);

                if (recordIndex * recordSize + recordSize > offsetRecords.Length) return null;

                int recordOffset = recordIndex * recordSize;
                ulong baseOffset = BinaryPrimitives.ReadUInt64BigEndian(offsetRecords.AsSpan(recordOffset, 8));

                ulong blockOffset = baseOffset;

                for (int i = 0; i < subIndex; i++)
                {
                    ushort sCompressedSize = BinaryPrimitives.ReadUInt16BigEndian(offsetRecords.AsSpan(recordOffset + 8 + i * 2, 2));
                    blockOffset += (ulong)(sCompressedSize + 1);
                }

                ushort currentCompressedSize = BinaryPrimitives.ReadUInt16BigEndian(offsetRecords.AsSpan(recordOffset + 8 + subIndex * 2, 2));
                int compressedSize = currentCompressedSize + 1;

                stream.Seek((long)(footer.sectionCompressedData.offset + blockOffset), SeekOrigin.Begin);

                if (compressedSize == BLOCK_SIZE)
                {
                    byte[] uncompressed = new byte[BLOCK_SIZE];

                    return stream.Read(uncompressed, 0, BLOCK_SIZE) == BLOCK_SIZE ? uncompressed : null;
                }

                byte[] compressed = new byte[compressedSize];

                if (stream.Read(compressed, 0, compressedSize) != compressedSize) return null;

                byte[] decompressed = new byte[BLOCK_SIZE];
                int decompressedSize = decompressor.Unwrap(compressed, decompressed);

                return decompressedSize == BLOCK_SIZE ? decompressed : null;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}