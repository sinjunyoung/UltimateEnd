using System;
using System.IO;

namespace UltimateEnd.SaveFile.Parsers
{
    public static class ParamSfoParser
    {
        private static readonly byte[] SfoMagic = [0x00, 0x50, 0x53, 0x46];

        public static byte[]? FindInStream(Stream stream, long maxSearchSize = 50 * 1024 * 1024)
        {
            var searchSize = Math.Min(maxSearchSize, stream.Length);
            var buffer = new byte[8192];

            stream.Seek(0, SeekOrigin.Begin);

            for (long offset = 0; offset < searchSize; offset += buffer.Length - 4)
            {
                stream.Seek(offset, SeekOrigin.Begin);
                var bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead < 4) break;

                for (int i = 0; i < bytesRead - 4; i++)
                {
                    if (buffer[i] == 0x00 && buffer[i + 1] == 0x50 && buffer[i + 2] == 0x53 && buffer[i + 3] == 0x46)
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

        public static string? ParseDiscId(byte[] sfoData)
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
                    var keyName = FileFormatUtils.ReadNullTerminatedString(sfoData, keyNameOffset);

                    if (keyName == "DISC_ID")
                    {
                        var valueOffset = dataTableOffset + dataOffset;
                        var discId = FileFormatUtils.ReadNullTerminatedString(sfoData, valueOffset);

                        if (!string.IsNullOrEmpty(discId)) return discId.ToUpper();
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        public static string? SearchInHunk(byte[] hunk)
        {
            for (int pos = 0; pos < hunk.Length - 4; pos++)
            {
                if (hunk[pos] == 0x00 && hunk[pos + 1] == 0x50 && hunk[pos + 2] == 0x53 && hunk[pos + 3] == 0x46)
                {
                    int sfoSize = Math.Min(20480, hunk.Length - pos);
                    byte[] sfoData = new byte[sfoSize];
                    Array.Copy(hunk, pos, sfoData, 0, sfoSize);

                    var discId = ParseDiscId(sfoData);

                    if (!string.IsNullOrEmpty(discId)) return discId;
                }
            }

            return null;
        }
    }
}