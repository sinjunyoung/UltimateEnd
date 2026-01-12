using System;
using System.IO;
using System.Text;

namespace UltimateEnd.SaveFile.Parsers
{
    public class GczParser : IFormatParser
    {
        private const uint GCZ_MAGIC = 0xB10BC001;

        public bool CanParse(string extension) => extension.ToLower() == ".gcz";

        public string? ParseGameId(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                byte[] headerBytes = new byte[32];

                if (stream.Read(headerBytes, 0, 32) != 32) return null;

                uint magic = BitConverter.ToUInt32(headerBytes, 0);
                ulong compressedDataSize = BitConverter.ToUInt64(headerBytes, 8);
                uint blockSize = BitConverter.ToUInt32(headerBytes, 24);
                uint numBlocks = BitConverter.ToUInt32(headerBytes, 28);

                if (magic != GCZ_MAGIC || numBlocks == 0) return null;

                byte[] offsetBytes = new byte[8];

                if (stream.Read(offsetBytes, 0, 8) != 8) return null;

                ulong firstBlockPointer = BitConverter.ToUInt64(offsetBytes, 0);
                bool isUncompressed = (firstBlockPointer & 0x8000000000000000UL) != 0;
                ulong actualOffset = firstBlockPointer & 0x7FFFFFFFFFFFFFFFUL;
                long dataOffset = 32 + (8 * numBlocks) + (4 * numBlocks);
                long absoluteBlockOffset = dataOffset + (long)actualOffset;

                stream.Seek(absoluteBlockOffset, SeekOrigin.Begin);

                byte[] decompressedData = new byte[blockSize];

                if (isUncompressed)
                {
                    if (stream.Read(decompressedData, 0, (int)blockSize) < 6) return null;
                }
                else
                {
                    long currentPos = stream.Position;
                    stream.Seek(32 + 8, SeekOrigin.Begin);

                    byte[] secondOffsetBytes = new byte[8];
                    ulong compressedSize;

                    if (stream.Read(secondOffsetBytes, 0, 8) == 8)
                    {
                        ulong secondBlockPointer = BitConverter.ToUInt64(secondOffsetBytes, 0);
                        ulong secondActualOffset = secondBlockPointer & 0x7FFFFFFFFFFFFFFFUL;
                        compressedSize = secondActualOffset - actualOffset;
                    }
                    else
                        compressedSize = compressedDataSize - actualOffset;

                    stream.Seek(currentPos, SeekOrigin.Begin);

                    byte[] compressedBlock = new byte[compressedSize];

                    if (stream.Read(compressedBlock, 0, (int)compressedSize) != (int)compressedSize) return null;

                    var decompressed = FileFormatUtils.DecompressZlib(compressedBlock, (int)blockSize);

                    if (decompressed == null || decompressed.Length < 6) return null;

                    decompressedData = decompressed;
                }

                return Encoding.ASCII.GetString(decompressedData, 0, 6);
            }
            catch
            {
                return null;
            }
        }
    }
}