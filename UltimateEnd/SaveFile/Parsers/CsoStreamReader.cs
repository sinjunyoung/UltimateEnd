using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace UltimateEnd.SaveFile.Parsers
{
    public class CsoStreamReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly BinaryReader _reader;
        private readonly uint[] _index;
        private readonly uint _blockSize;
        private readonly byte _ver;
        private readonly byte _align;
        private readonly uint _blockShift;
        private readonly uint _numBlocks;

        public CsoStreamReader(string filePath)
        {
            _stream = File.OpenRead(filePath);
            _reader = new BinaryReader(_stream);

            byte[] magic = _reader.ReadBytes(4);

            if (Encoding.ASCII.GetString(magic) != "CISO") throw new InvalidDataException("Not a valid CSO file");

            uint headerSize = _reader.ReadUInt32();
            ulong totalBytes = _reader.ReadUInt64();
            _blockSize = _reader.ReadUInt32();
            _ver = _reader.ReadByte();
            _align = _reader.ReadByte();
            _reader.ReadBytes(2);

            _numBlocks = (uint)(totalBytes / 2048);
            uint numFrames = (uint)((totalBytes + _blockSize - 1) / _blockSize);

            _blockShift = 0;

            for (uint i = _blockSize; i > 0x800; i >>= 1) _blockShift++;

            _stream.Position = headerSize > 0 ? headerSize : 24;
            _index = new uint[numFrames + 1];

            for (uint i = 0; i <= numFrames; i++) _index[i] = _reader.ReadUInt32();
        }

        public byte[]? ReadSector(uint sectorNumber)
        {
            if (sectorNumber >= _numBlocks) return null;

            uint frameNumber = sectorNumber >> (int)_blockShift;
            uint idx = _index[frameNumber];
            uint indexPos = idx & 0x7FFFFFFF;
            uint nextIndexPos = _index[frameNumber + 1] & 0x7FFFFFFF;

            ulong compressedReadPos = (ulong)indexPos << _align;
            ulong compressedReadEnd = (ulong)nextIndexPos << _align;
            uint compressedReadSize = (uint)(compressedReadEnd - compressedReadPos);
            uint compressedOffset = (uint)((sectorNumber & ((1 << (int)_blockShift) - 1)) * 2048);

            bool plain = (idx & 0x80000000) != 0;

            if (_ver >= 2) plain = compressedReadSize >= _blockSize;

            byte[] blockData = new byte[2048];

            if (plain)
            {
                _stream.Seek((long)(compressedReadPos + compressedOffset), SeekOrigin.Begin);
                int readSize = _stream.Read(blockData, 0, 2048);

                if (readSize < 2048) Array.Clear(blockData, readSize, 2048 - readSize);
            }
            else
            {
                _stream.Seek((long)compressedReadPos, SeekOrigin.Begin);
                byte[] compressed = _reader.ReadBytes((int)compressedReadSize);

                byte[]? frameData = DecompressZlib(compressed, (int)_blockSize);

                if (frameData != null)
                {
                    int copySize = Math.Min(2048, frameData.Length - (int)compressedOffset);

                    if (copySize > 0) Array.Copy(frameData, compressedOffset, blockData, 0, copySize);
                }
                else
                    return null;
            }

            return blockData;
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

        public void Dispose()
        {
            _reader?.Dispose();
            _stream?.Dispose();
        }
    }
}