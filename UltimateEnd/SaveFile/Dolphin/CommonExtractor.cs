using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UltimateEnd.SaveFile.Dolphin
{

    internal static class CommonExtractor
    {
        private const uint GCZ_MAGIC = 0xB10BC001;
        private const uint WIA_MAGIC = 0x01414957;
        private const uint RVZ_MAGIC = 0x015A5652;

        public static string? ExtractFromIso(string isoPath)
        {
            try
            {
                using var stream = File.OpenRead(isoPath);
                byte[] gameIdBytes = new byte[6];
                stream.ReadExactly(gameIdBytes, 0, 6);

                return Encoding.ASCII.GetString(gameIdBytes);
            }
            catch
            {
                return null;
            }
        }

        public static string? ExtractFromGcz(string gczPath)
        {
            try
            {
                using var stream = File.OpenRead(gczPath);
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
                    if (stream.Read(decompressedData, 0, (int)blockSize) < 6)
                        return null;
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

                    try
                    {
                        using var memStream = new MemoryStream(compressedBlock);
                        memStream.ReadByte();
                        memStream.ReadByte();

                        using var deflateStream = new DeflateStream(memStream, CompressionMode.Decompress);
                        int bytesRead = deflateStream.Read(decompressedData, 0, (int)blockSize);

                        if (bytesRead < 6)
                            return null;
                    }
                    catch
                    {
                        return null;
                    }
                }

                return Encoding.ASCII.GetString(decompressedData, 0, 6);
            }
            catch
            {
                return null;
            }
        }

        public static string? ExtractFromRvz(string rvzPath)
        {
            try
            {
                using var stream = File.OpenRead(rvzPath);
                byte[] header1 = new byte[0x48];

                if (stream.Read(header1, 0, 0x48) != 0x48) return null;

                uint magic = BitConverter.ToUInt32(header1, 0);

                if (magic != WIA_MAGIC && magic != RVZ_MAGIC) return null;

                uint header2Size = BinaryPrimitives.ReverseEndianness(BitConverter.ToUInt32(header1, 0x0C));

                byte[] header2 = new byte[header2Size];

                if (stream.Read(header2, 0, (int)header2Size) != (int)header2Size) return null;

                if (header2Size < 0x10 + 6) return null;

                return Encoding.ASCII.GetString(header2, 0x10, 6);
            }
            catch
            {
                return null;
            }
        }
    }
}