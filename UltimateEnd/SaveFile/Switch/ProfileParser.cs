using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace UltimateEnd.SaveFile.Switch
{
    public class ProfileParser
    {
        private const string PROFILES_RELATIVE_PATH = "nand/system/save/8000000000000010/su/avators/profiles.dat";
        private const int HEADER_SIZE = 0x10;
        private const int USER_BLOCK_SIZE = 0xC8; // 200 bytes
        private const int MAX_USERS = 8;

        public static string GetProfilesPath(string basePath) => Path.Combine(basePath, PROFILES_RELATIVE_PATH);

        public static List<string> ParseProfileUUIDs(string profilesPath)
        {
            var uuids = new List<string>();

            if (!File.Exists(profilesPath)) return uuids;

            try
            {
                using var fs = new FileStream(profilesPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fs);

                reader.BaseStream.Seek(HEADER_SIZE, SeekOrigin.Begin);

                for (int i = 0; i < MAX_USERS; i++)
                {
                    if (reader.BaseStream.Position + USER_BLOCK_SIZE > reader.BaseStream.Length) break;

                    byte[] userId1Bytes = reader.ReadBytes(16);
                    byte[] userId2Bytes = reader.ReadBytes(16);

                    if (IsValidUUID(userId1Bytes))
                    {
                        string uuid = BytesToHex(userId1Bytes);

                        if (!uuids.Contains(uuid)) uuids.Add(uuid);
                    }

                    reader.BaseStream.Seek(USER_BLOCK_SIZE - 32, SeekOrigin.Current);
                }

                return uuids;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"profiles.dat 파싱 오류: {ex.Message}");
                return uuids;
            }
        }

        public static List<ProfileInfo> ParseProfiles(string profilesPath)
        {
            var profiles = new List<ProfileInfo>();

            if (!File.Exists(profilesPath)) return profiles;

            try
            {
                using var fs = new FileStream(profilesPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(fs);

                reader.BaseStream.Seek(HEADER_SIZE, SeekOrigin.Begin);

                for (int i = 0; i < MAX_USERS; i++)
                {
                    if (reader.BaseStream.Position + USER_BLOCK_SIZE > reader.BaseStream.Length) break;

                    long blockStart = reader.BaseStream.Position;

                    byte[] userId1Bytes = reader.ReadBytes(16);

                    byte[] userId2Bytes = reader.ReadBytes(16);

                    if (!IsValidUUID(userId1Bytes))
                    {
                        reader.BaseStream.Seek(blockStart + USER_BLOCK_SIZE, SeekOrigin.Begin);
                        continue;
                    }

                    long lastEditTime = reader.ReadInt64();

                    byte[] accountNameBytes = reader.ReadBytes(32);
                    string accountName = Encoding.UTF8.GetString(accountNameBytes).TrimEnd('\0');

                    reader.ReadInt32();

                    int iconId = reader.ReadInt32();

                    byte backgroundColorId = reader.ReadByte();

                    var profile = new ProfileInfo
                    {
                        UserId1 = BytesToHex(userId1Bytes),
                        UserId2 = BytesToHex(userId2Bytes),
                        LastEditTime = lastEditTime,
                        AccountName = accountName,
                        IconId = iconId,
                        BackgroundColorId = backgroundColorId
                    };

                    profiles.Add(profile);

                    reader.BaseStream.Seek(blockStart + USER_BLOCK_SIZE, SeekOrigin.Begin);
                }

                return profiles;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"profiles.dat 파싱 오류: {ex.Message}");
                return profiles;
            }
        }

        private static bool IsValidUUID(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 16) return false;

            if (bytes.All(b => b == 0)) return false;

            if (bytes.All(b => b == 0xFF)) return false;

            return true;
        }

        private static string BytesToHex(byte[] bytes)
        {
            Array.Reverse(bytes);

            var sb = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes) sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        public static byte[]? BackupProfilesFile(string basePath)
        {
            string profilesPath = GetProfilesPath(basePath);

            if (!File.Exists(profilesPath)) return null;

            return File.ReadAllBytes(profilesPath);
        }

        public static void RestoreProfilesFile(string basePath, byte[] profilesData)
        {
            string profilesPath = GetProfilesPath(basePath);
            string? directory = Path.GetDirectoryName(profilesPath);

            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            File.WriteAllBytes(profilesPath, profilesData);
        }
    }
}