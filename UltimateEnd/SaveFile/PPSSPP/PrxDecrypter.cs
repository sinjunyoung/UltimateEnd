using System;
using System.Text;

namespace UltimateEnd.SaveFile.PPSSPP
{
    public static class PrxDecrypter
    {
        private const uint PSP_MODULE_INFO_MAGIC = 0x464C457F;
        private const uint PRX_MAGIC = 0x5053507E;
        private static readonly GameIdExtractor _extractor = new();

        public static bool IsEncrypted(byte[] data)
        {
            if (data == null || data.Length < 0x150) return false;

            uint magic = BitConverter.ToUInt32(data, 0);

            if (magic == PRX_MAGIC) return true;

            if (magic == PSP_MODULE_INFO_MAGIC) return false;

            return false;
        }

        public static byte[]? PartialDecrypt(byte[] encrypted)
        {
            if (encrypted == null || encrypted.Length < 0x150) return null;

            try
            {
                if (!IsEncrypted(encrypted)) return encrypted;

                uint tag = BitConverter.ToUInt32(encrypted, 0xD0);
                int decryptSize = BitConverter.ToInt32(encrypted, 0xB0);

                if (decryptSize <= 0 || decryptSize > encrypted.Length) return null;

                return TrySimpleDecryption(encrypted);
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? TrySimpleDecryption(byte[] data)
        {
            byte[][] commonKeys = [[0x00], [0xFF], [0x55], [0xAA],];

            byte[] result = new byte[data.Length];
            Array.Copy(data, result, data.Length);

            foreach (var key in commonKeys)
            {
                byte[] temp = new byte[data.Length];

                for (int i = 0; i < data.Length; i++)
                    temp[i] = (byte)(data[i] ^ key[i % key.Length]);

                if (ContainsSavePattern(temp)) return temp;
            }

            return result;
        }

        private static bool ContainsSavePattern(byte[] data)
        {
            string[] patterns = ["PSP/SAVEDATA/", "ms0:/PSP/SAVEDATA/", "host0:/PSP/SAVEDATA/",];

            foreach (var pattern in patterns)
            {
                var patternBytes = Encoding.ASCII.GetBytes(pattern);

                if (IndexOf(data, patternBytes) >= 0) return true;
            }

            return false;
        }

        private static int IndexOf(byte[] data, byte[] pattern)
        {
            for (int i = 0; i < data.Length - pattern.Length; i++)
            {
                bool found = true;

                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found) return i;
            }

            return -1;
        }

        public static string? ExtractSaveFolderId(byte[] decryptedData)
        {
            if (decryptedData == null || decryptedData.Length < 100) return null;

            var pathPatterns = new[]
            {
                Encoding.ASCII.GetBytes("ms0:/PSP/SAVEDATA/"),
                Encoding.ASCII.GetBytes("PSP/SAVEDATA/"),
                Encoding.ASCII.GetBytes("host0:/PSP/SAVEDATA/"),
                Encoding.ASCII.GetBytes("SAVEDATA/"),
            };

            foreach (var pattern in pathPatterns)
            {
                int index = IndexOf(decryptedData, pattern);

                if (index >= 0)
                {
                    int idStart = index + pattern.Length;
                    int idEnd = idStart;

                    while (idEnd < decryptedData.Length && idEnd < idStart + 15)
                    {
                        byte b = decryptedData[idEnd];

                        if (b >= 0x30 && b <= 0x39 || b >= 0x41 && b <= 0x5A || b >= 0x61 && b <= 0x7A || b == 0x5F)
                            idEnd++;
                        else
                            break;
                    }

                    if (idEnd > idStart)
                    {
                        string gameId = Encoding.ASCII.GetString(decryptedData, idStart, idEnd - idStart);

                        if ((gameId.Length == 9 || gameId.Length == 11) && _extractor.IsValidGameId(gameId)) return gameId.ToUpper();
                    }
                }
            }

            var validPrefixes = new[]
            {
                "ULUS", "UCUS", "NPUZ", "NPUX", "NPUF", "NPUH", "NPUG",
                "ULES", "UCES", "NPEZ", "NPEX", "NPEH", "NPEG",
                "ULJS", "ULJM", "UCJS", "UCJM", "UCJB", "NPJJ", "NPJH", "NPJG",
                "ULKS", "UCKS", "NPHH", "NPHG",
                "ULAS", "UCAS", "NPHZ"
            };

            foreach (var prefix in validPrefixes)
            {
                var prefixBytes = Encoding.ASCII.GetBytes(prefix);
                int index = IndexOf(decryptedData, prefixBytes);

                if (index >= 0 && index + 9 <= decryptedData.Length)
                {
                    string gameId = Encoding.ASCII.GetString(decryptedData, index, 9);

                    if (_extractor.IsValidGameId(gameId))
                    {
                        if (index + 12 <= decryptedData.Length && decryptedData[index + 9] == 0x5F)
                        {
                            string fullId = Encoding.ASCII.GetString(decryptedData, index, 12);

                            if (_extractor.IsValidGameId(fullId)) return fullId.ToUpper();
                        }

                        return gameId.ToUpper();
                    }
                }
            }

            return null;
        }
    }
}