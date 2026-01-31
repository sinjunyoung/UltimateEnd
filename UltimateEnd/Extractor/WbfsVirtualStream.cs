using System;
using System.IO;

namespace UltimateEnd.Extractor
{
    internal class WbfsVirtualStream(Stream baseStream, ushort[] wlbaTable, long wbfsSectorSize, int wbfsSectorShift) : Stream
    {
        private long _position = 0;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => 143432L * 2 * 0x8000;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (count > 0 && _position < Length)
            {
                long baseCluster = _position >> wbfsSectorShift;

                if (baseCluster >= wlbaTable.Length) break;

                long clusterAddress = wbfsSectorSize * wlbaTable[baseCluster];
                long clusterOffset = _position & (wbfsSectorSize - 1);

                long finalAddress;

                if (baseCluster == 0)
                {
                    if (clusterOffset >= 0x100)
                    {
                        clusterOffset -= 0x100;
                        clusterAddress = wbfsSectorSize * wlbaTable[1];
                        finalAddress = clusterAddress + clusterOffset;
                    }
                    else
                        finalAddress = 0x200 + clusterOffset;
                }
                else
                    finalAddress = clusterAddress + clusterOffset;

                long tillEndOfSector = wbfsSectorSize - clusterOffset;
                int toRead = (int)Math.Min(Math.Min(count, tillEndOfSector), int.MaxValue);

                baseStream.Seek(finalAddress, SeekOrigin.Begin);
                int read = baseStream.Read(buffer, offset, toRead);

                if (read == 0) break;

                _position += read;
                offset += read;
                count -= read;
                totalRead += read;
            }

            return totalRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    _position = Length + offset;
                    break;
            }
            return _position;
        }

        public override void Flush() { }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}