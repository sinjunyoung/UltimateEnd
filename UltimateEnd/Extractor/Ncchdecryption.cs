using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace UltimateEnd.Extractor
{
    public static class NCCHDecryption
    {
        private static Dictionary<string, byte[]> _keys = [];
        private static byte[] _generatorConstant;
        private static bool _initialized = false;

        public static bool Initialize(string aesKeysPath)
        {
            if (_initialized) return true;

            try
            {
                if (!File.Exists(aesKeysPath)) return false;

                foreach (var line in File.ReadLines(aesKeysPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

                    var parts = line.Split('=');

                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        _keys[key] = HexToBytes(value);
                    }
                }

                if (_keys.TryGetValue("generator", out var gen) || _keys.TryGetValue("generatorConstant", out gen)) _generatorConstant = gen;

                _initialized = _keys.Count > 0;

                return _initialized;
            }
            catch
            {
                return false;
            }
        }

        public static Stream CreateDecryptedStream(string filePath, NCCHHeader header, long ncchOffset)
        {
            if (!_initialized || header.Flags.NoCrypto)
            {
                var fs = File.OpenRead(filePath);
                fs.Seek(ncchOffset, SeekOrigin.Begin);
                return fs;
            }

            int keySlot = DetermineKeySlot(header);
            var key = GetNormalKey(keySlot, header);

            if (key == null) return null;

            var iv = new byte[16];
            var reversedPartitionId = header.PartitionId.Reverse().ToArray();
            Array.Copy(reversedPartitionId, 0, iv, 0, 8);
            iv[8] = 0x02;

            var baseStream = File.OpenRead(filePath);

            return new AESCTRStream(baseStream, key, iv, ncchOffset);
        }

        private static int DetermineKeySlot(NCCHHeader header)
        {
            if (header.Flags.FixedKey) return 0x2C;

            return (header.ProgramIdHigh >> 12) switch
            {
                0x0004 => 0x2C,
                0x0000 => 0x2C,
                _ => header.SecondaryKeySlot != 0 ? header.SecondaryKeySlot : 0x2C
            };
        }

        private static byte[] GetNormalKey(int slot, NCCHHeader header)
        {
            string keyXName = $"slot0x{slot:X}KeyX";

            if (!_keys.TryGetValue(keyXName, out var keyX)) return null;

            var keyY = BuildKeyY(header);
            var normalKey = GenerateNormalKey(keyX, keyY);

            return normalKey;
        }

        private static byte[] BuildKeyY(NCCHHeader header) => [..header.Signature.Take(16)];

        private static byte[] GenerateNormalKey(byte[] keyX, byte[] keyY)
        {
            if (_generatorConstant == null) return null;

            var temp = RotateLeft128(keyX, 2);
            temp = Xor128(temp, keyY);
            temp = Add128(temp, _generatorConstant);

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

            for (int i = 0; i < 16; i++) result[i] = (byte)(a[i] ^ b[i]);

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
            return [.. Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16))];
        }
    }   
}