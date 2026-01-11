using System;
using System.IO;
using System.Text;

namespace UltimateEnd.SaveFile.Dolphin
{
    public static class WiiIdExtractor
    {
        public static string? ExtractGameId(string isoPath)
        {
            if (!File.Exists(isoPath)) return null;

            string ext = Path.GetExtension(isoPath).ToLower();

            if (ext == ".gcz")
            {
                var titleId = CommonExtractor.ExtractFromGcz(isoPath);

                return IsValidWiiId(titleId) ? titleId : null;
            }

            if (ext == ".rvz" || ext == ".wia")
            {
                var titleId = CommonExtractor.ExtractFromRvz(isoPath);

                return IsValidWiiId(titleId) ? titleId : null;
            }

            if (ext == ".wbfs") return ExtractFromWbfs(isoPath);

            if (ext == ".wad") return ExtractFromWad(isoPath);

            var id = CommonExtractor.ExtractFromIso(isoPath);

            return IsValidWiiId(id) ? id : null;
        }

        private static bool IsValidWiiId(string? titleId)
        {
            if (string.IsNullOrWhiteSpace(titleId) || titleId.Length < 6) return false;

            return titleId[0] == 'R' || titleId[0] == 'S';
        }

        private static string? ExtractFromWbfs(string wbfsPath)
        {
            try
            {
                using var stream = File.OpenRead(wbfsPath);
                using var reader = new BinaryReader(stream);

                stream.Seek(0x00, SeekOrigin.Begin);
                byte[] magic = reader.ReadBytes(4);
                string magicStr = Encoding.ASCII.GetString(magic);

                if (magicStr != "WBFS") return null;

                stream.Seek(0x200, SeekOrigin.Begin);
                byte[] titleIdBytes = reader.ReadBytes(6);
                string titleId = Encoding.ASCII.GetString(titleIdBytes);

                return IsValidWiiId(titleId) ? titleId : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ExtractFromWad(string wadPath)
        {
            try
            {
                using var stream = File.OpenRead(wadPath);
                using var reader = new BinaryReader(stream);

                stream.Seek(0x00, SeekOrigin.Begin);
                uint headerSize = ReadBigEndianUInt32(reader);

                stream.Seek(0x1E0, SeekOrigin.Begin);
                byte[] titleIdBytes = reader.ReadBytes(8);
                string titleId = Encoding.ASCII.GetString(titleIdBytes, 4, 4);

                if (titleId.Length == 4) return titleId + "00";

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static uint ReadBigEndianUInt32(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);

            if (BitConverter.IsLittleEndian) Array.Reverse(bytes);

            return BitConverter.ToUInt32(bytes, 0);
        }
    }
}