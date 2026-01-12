using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UltimateEnd.SaveFile.Parsers
{
    public class PspCsoParser : IFormatParser
    {
        public bool CanParse(string extension) => extension.ToLower() == ".cso";

        public string? ParseGameId(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var reader = new BinaryReader(stream);

                byte[] magic = reader.ReadBytes(4);

                if (Encoding.ASCII.GetString(magic) != "CISO") return null;

                uint headerSize = reader.ReadUInt32();
                ulong totalBytes = reader.ReadUInt64();
                uint blockSize = reader.ReadUInt32();
                byte ver = reader.ReadByte();
                byte align = reader.ReadByte();
                reader.ReadBytes(2);

                uint numBlocks = (uint)(totalBytes / 2048);
                uint numFrames = (uint)((totalBytes + blockSize - 1) / blockSize);

                uint blockShift = 0;

                for (uint i = blockSize; i > 0x800; i >>= 1) blockShift++;

                stream.Position = headerSize > 0 ? headerSize : 24;

                uint[] index = new uint[numFrames + 1];

                for (uint i = 0; i <= numFrames; i++) index[i] = reader.ReadUInt32();

                int searchBlocks = Math.Min(5000, (int)numBlocks);
                using var decompressed = new MemoryStream();

                for (int blockNumber = 0; blockNumber < searchBlocks; blockNumber++)
                {
                    uint frameNumber = (uint)(blockNumber >> (int)blockShift);
                    uint idx = index[frameNumber];
                    uint indexPos = idx & 0x7FFFFFFF;
                    uint nextIndexPos = index[frameNumber + 1] & 0x7FFFFFFF;

                    ulong compressedReadPos = (ulong)indexPos << align;
                    ulong compressedReadEnd = (ulong)nextIndexPos << align;
                    uint compressedReadSize = (uint)(compressedReadEnd - compressedReadPos);
                    uint compressedOffset = (uint)((blockNumber & ((1 << (int)blockShift) - 1)) * 2048);

                    bool plain = (idx & 0x80000000) != 0;

                    if (ver >= 2) plain = compressedReadSize >= blockSize;

                    byte[] blockData = new byte[2048];

                    if (plain)
                    {
                        stream.Seek((long)(compressedReadPos + compressedOffset), SeekOrigin.Begin);
                        int readSize = stream.Read(blockData, 0, 2048);

                        if (readSize < 2048) Array.Clear(blockData, readSize, 2048 - readSize);
                    }
                    else
                    {
                        stream.Seek((long)compressedReadPos, SeekOrigin.Begin);
                        byte[] compressed = reader.ReadBytes((int)compressedReadSize);

                        byte[] frameData = DecompressZlib(compressed, (int)blockSize);

                        if (frameData != null)
                            Array.Copy(frameData, compressedOffset, blockData, 0, Math.Min(2048, frameData.Length - (int)compressedOffset));
                        else
                            continue;
                    }

                    decompressed.Write(blockData, 0, blockData.Length);
                }

                if (decompressed.Length < 100) return null;

                decompressed.Seek(0, SeekOrigin.Begin);
                byte[]? paramSfoData = ParamSfoParser.FindInStream(decompressed);

                return paramSfoData != null ? ParamSfoParser.ParseDiscId(paramSfoData) : null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? DecompressZlib(byte[] compressed, int expectedSize)
        {
            try
            {
                byte[] decompressed = new byte[expectedSize];

                using var compressedStream = new MemoryStream(compressed);
                using var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress);

                int totalRead = 0;
                int bytesRead;

                while (totalRead < expectedSize && (bytesRead = deflateStream.Read(decompressed, totalRead, expectedSize - totalRead)) > 0)
                    totalRead += bytesRead;

                return totalRead > 0 ? decompressed : null;
            }
            catch
            {
                return null;
            }
        }
    }
}