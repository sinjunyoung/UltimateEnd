using System;
using System.IO;
using System.Text;

namespace UltimateEnd.SaveFile.Dolphin
{
    public class WiiIdExtractor : IGameIdExtractor
    {
        private readonly DolphinFormatParserRegistry _parserRegistry = new();

        public string? ExtractGameId(string romPath)
        {
            if (!File.Exists(romPath)) return null;

            string ext = Path.GetExtension(romPath).ToLower();

            if (ext == ".wbfs")
                return ExtractFromWbfs(romPath);
            if (ext == ".wad")
                return ExtractFromWad(romPath);

            var titleId = _parserRegistry.ParseGameId(romPath);

            return IsValidGameId(titleId) ? titleId : null;
        }

        public bool IsValidGameId(string? titleId)
        {
            if (string.IsNullOrWhiteSpace(titleId) || titleId.Length < 6) return false;

            return titleId[0] == 'R' || titleId[0] == 'S';
        }

        private string? ExtractFromWbfs(string wbfsPath)
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

                return IsValidGameId(titleId) ? titleId : null;
            }
            catch
            {
                return null;
            }
        }

        private string? ExtractFromWad(string wadPath)
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