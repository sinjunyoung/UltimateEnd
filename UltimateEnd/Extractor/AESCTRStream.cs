using System;
using System.IO;
using System.Security.Cryptography;

namespace UltimateEnd.Extractor
{
    public class AESCTRStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly byte[] _key;
        private readonly byte[] _initialCTR;
        private readonly Aes _aes;
        private readonly ICryptoTransform _encryptor;
        private readonly long _ncchOffset;
        private long _position;

        public AESCTRStream(Stream baseStream, byte[] key, byte[] ctr, long ncchOffset)
        {
            _baseStream = baseStream;
            _key = key;
            _initialCTR = ctr;
            _ncchOffset = ncchOffset;

            _aes = Aes.Create();
            _aes.Mode = CipherMode.ECB;
            _aes.Key = key;
            _aes.Padding = PaddingMode.None;
            _encryptor = _aes.CreateEncryptor();
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => _baseStream.Length - _ncchOffset;

        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var encrypted = new byte[count];
            _baseStream.Position = _ncchOffset + _position;
            int bytesRead = _baseStream.Read(encrypted, 0, count);

            if (bytesRead == 0) return 0;

            for (int i = 0; i < bytesRead; i++)
            {
                long blockNum = (_position + i + 0x200) / 16;
                int blockOffset = (int)((_position + i) % 16);

                var counter = GenerateCounter(blockNum);
                var keystream = new byte[16];
                _encryptor.TransformBlock(counter, 0, 16, keystream, 0);

                buffer[offset + i] = (byte)(encrypted[i] ^ keystream[blockOffset]);
            }

            _position += bytesRead;

            return bytesRead;
        }

        private byte[] GenerateCounter(long blockNumber)
        {
            var counter = new byte[16];
            Array.Copy(_initialCTR, counter, 16);

            for (int i = 15; i >= 0; i--)
            {
                long carry = blockNumber & 0xFF;
                counter[i] = (byte)((counter[i] + carry) & 0xFF);
                blockNumber >>= 8;

                if (blockNumber == 0) break;
            }

            return counter;
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _encryptor?.Dispose();
                _aes?.Dispose();
                _baseStream?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}