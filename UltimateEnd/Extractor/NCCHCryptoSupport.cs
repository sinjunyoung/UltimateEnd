using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace UltimateEnd.Extractor
{
    /// <summary>
    /// NCCH 암호화 지원 - aes_keys.txt만 필요
    /// </summary>
    public static class NCCHDecryption
    {
        private static Dictionary<string, byte[]> _keys = new();
        private static byte[] _generatorConstant;
        private static bool _initialized = false;

        /// <summary>
        /// aes_keys.txt 로드 (한 번만 호출)
        /// </summary>
        public static bool Initialize(string aesKeysPath)
        {
            if (_initialized) return true;

            try
            {
                if (!File.Exists(aesKeysPath))
                    return false;

                foreach (var line in File.ReadLines(aesKeysPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        _keys[key] = HexToBytes(value);
                    }
                }

                // Generator constant 로드 (Normal Key 생성에 필요)
                if (_keys.TryGetValue("generator", out var gen) ||
                    _keys.TryGetValue("generatorConstant", out gen))
                {
                    _generatorConstant = gen;
                }

                _initialized = _keys.Count > 0;
                return _initialized;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// NCCH 파일 복호화 스트림 생성
        /// </summary>
        public static Stream CreateDecryptedStream(string filePath, NCCHHeader header)
        {
            if (!_initialized)
                return null;

            // 암호화되지 않은 파일
            if (header.Flags.NoCrypto)
                return File.OpenRead(filePath);

            // 암호화된 파일 - 적절한 키 슬롯 선택
            int keySlot = DetermineKeySlot(header);
            var key = GetNormalKey(keySlot, header);

            if (key == null)
                return null;

            // AES-CTR 스트림 생성
            var iv = BuildCTR(header);
            return new AESCTRStream(File.OpenRead(filePath), key, iv);
        }

        /// <summary>
        /// 키 슬롯 결정
        /// </summary>
        private static int DetermineKeySlot(NCCHHeader header)
        {
            // Secondary key slot 사용
            if (header.Flags.FixedKey)
                return 0x2C; // Secure1

            // 일반적인 경우
            switch (header.ProgramIdHigh >> 12)
            {
                case 0x0004: // Application
                    return 0x2C; // NCCHSecure1
                case 0x0000: // System
                    return 0x2C;
                default:
                    return header.SecondaryKeySlot;
            }
        }

        /// <summary>
        /// Normal Key 생성 (KeyX + KeyY)
        /// </summary>
        private static byte[] GetNormalKey(int slot, NCCHHeader header)
        {
            // KeyX 가져오기
            string keyXName = $"slot0x{slot:X}KeyX";
            if (!_keys.TryGetValue(keyXName, out var keyX))
                return null;

            // KeyY 생성 (NCCH 헤더에서)
            var keyY = BuildKeyY(header);

            // Normal Key = ROL((ROL(KeyX, 2) XOR KeyY) + Constant, 87)
            return GenerateNormalKey(keyX, keyY);
        }

        /// <summary>
        /// KeyY 생성
        /// </summary>
        private static byte[] BuildKeyY(NCCHHeader header)
        {
            // NCCH의 KeyY는 Signature가 아니라 특정 오프셋에서 읽음
            // 실제로는 NCCH 헤더의 0x00~0x0F (Signature의 마지막 16바이트)를 사용
            return header.Signature.Skip(0xF0).Take(16).ToArray();
        }

        /// <summary>
        /// CTR (Counter) 생성
        /// </summary>
        private static byte[] BuildCTR(NCCHHeader header)
        {
            var ctr = new byte[16];

            // Partition ID (8바이트)
            Array.Copy(header.PartitionId, 0, ctr, 0, 8);

            // 나머지는 0으로 채움 (섹션 오프셋에 따라 증가)
            return ctr;
        }

        /// <summary>
        /// Normal Key 생성 알고리즘
        /// </summary>
        private static byte[] GenerateNormalKey(byte[] keyX, byte[] keyY)
        {
            if (_generatorConstant == null)
                return null;

            // 1. ROL(KeyX, 2)
            var temp = RotateLeft128(keyX, 2);

            // 2. XOR with KeyY
            temp = Xor128(temp, keyY);

            // 3. Add generator constant
            temp = Add128(temp, _generatorConstant);

            // 4. ROL(result, 87)
            return RotateLeft128(temp, 87);
        }

        private static byte[] RotateLeft128(byte[] data, int bits)
        {
            var result = new byte[16];
            int bytes = bits / 8;
            int bitShift = bits % 8;

            for (int i = 0; i < 16; i++)
            {
                int srcIdx = (i + bytes) % 16;
                result[i] = (byte)(data[srcIdx] << bitShift);

                if (bitShift > 0)
                {
                    int nextIdx = (srcIdx + 1) % 16;
                    result[i] |= (byte)(data[nextIdx] >> (8 - bitShift));
                }
            }

            return result;
        }

        private static byte[] Xor128(byte[] a, byte[] b)
        {
            var result = new byte[16];
            for (int i = 0; i < 16; i++)
                result[i] = (byte)(a[i] ^ b[i]);
            return result;
        }

        private static byte[] Add128(byte[] a, byte[] b)
        {
            var result = new byte[16];
            int carry = 0;

            for (int i = 15; i >= 0; i--)
            {
                int sum = a[i] + b[i] + carry;
                result[i] = (byte)(sum & 0xFF);
                carry = sum >> 8;
            }

            return result;
        }

        private static byte[] HexToBytes(string hex)
        {
            return Enumerable.Range(0, hex.Length / 2)
                .Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))
                .ToArray();
        }
    }

    /// <summary>
    /// NCCH 헤더 구조
    /// </summary>
    public class NCCHHeader
    {
        public byte[] Signature = new byte[0x100];
        public uint Magic;
        public uint ContentSize;
        public byte[] PartitionId = new byte[8];
        public ushort MakerCode;
        public ushort Version;
        public ulong ProgramId;
        public byte SecondaryKeySlot;
        public NCCHFlags Flags = new();

        public ulong ProgramIdHigh => ProgramId >> 32;

        public static NCCHHeader Read(BinaryReader reader)
        {
            var header = new NCCHHeader();

            reader.Read(header.Signature, 0, 0x100);
            header.Magic = reader.ReadUInt32();
            header.ContentSize = reader.ReadUInt32();
            reader.Read(header.PartitionId, 0, 8);
            header.MakerCode = reader.ReadUInt16();
            header.Version = reader.ReadUInt16();

            reader.BaseStream.Seek(4, SeekOrigin.Current); // Skip reserved

            header.ProgramId = reader.ReadUInt64();

            reader.BaseStream.Seek(0x10, SeekOrigin.Current); // Skip reserved
            reader.BaseStream.Seek(0x20, SeekOrigin.Current); // Skip logo hash
            reader.BaseStream.Seek(0x10, SeekOrigin.Current); // Skip product code
            reader.BaseStream.Seek(0x20, SeekOrigin.Current); // Skip extended header hash
            reader.BaseStream.Seek(4, SeekOrigin.Current); // Skip extended header size
            reader.BaseStream.Seek(4, SeekOrigin.Current); // Skip reserved
            reader.BaseStream.Seek(3, SeekOrigin.Current); // Skip reserved flags

            header.SecondaryKeySlot = reader.ReadByte();
            reader.BaseStream.Seek(1, SeekOrigin.Current); // Platform

            byte flagByte = reader.ReadByte();
            header.Flags.NoCrypto = (flagByte & 0x04) != 0;
            header.Flags.FixedKey = (flagByte & 0x01) != 0;
            header.Flags.SeedCrypto = (flagByte & 0x20) != 0;

            return header;
        }
    }

    public class NCCHFlags
    {
        public bool NoCrypto;
        public bool FixedKey;
        public bool SeedCrypto;
    }

    /// <summary>
    /// AES-CTR 스트림 (이전과 동일)
    /// </summary>
    public class AESCTRStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly byte[] _key;
        private readonly byte[] _initialCTR;
        private readonly Aes _aes;
        private readonly ICryptoTransform _encryptor;
        private long _position;

        public AESCTRStream(Stream baseStream, byte[] key, byte[] ctr)
        {
            _baseStream = baseStream;
            _key = key;
            _initialCTR = ctr;

            _aes = Aes.Create();
            _aes.Mode = CipherMode.ECB;
            _aes.Key = key;
            _aes.Padding = PaddingMode.None;
            _encryptor = _aes.CreateEncryptor();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _baseStream.Length;

        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var encrypted = new byte[count];
            _baseStream.Position = _position;
            int bytesRead = _baseStream.Read(encrypted, 0, count);

            if (bytesRead == 0)
                return 0;

            for (int i = 0; i < bytesRead; i++)
            {
                long blockNum = (_position + i) / 16;
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
                if (blockNumber == 0)
                    break;
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