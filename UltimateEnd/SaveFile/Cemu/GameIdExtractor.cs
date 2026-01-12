using System;
using System.IO;

namespace UltimateEnd.SaveFile.Cemu
{
    public class GameIdExtractor : IGameIdExtractor
    {
        private readonly CemuFormatParserRegistry _parserRegistry = new();

        public string? ExtractGameId(string romPath)
        {
            if (!File.Exists(romPath)) return null;

            string ext = Path.GetExtension(romPath).ToLower();

            if (ext != ".wua") throw new NotSupportedException($"{ext.ToUpperInvariant()}는 지원하지 않습니다.\r\n.WUA만 지원합니다.");

            var titleId = _parserRegistry.ParseGameId(romPath);

            return IsValidGameId(titleId) ? titleId : null;
        }

        public bool IsValidGameId(string? gameId)
        {
            if (string.IsNullOrEmpty(gameId) || gameId.Length != 8) return false;

            foreach (char c in gameId)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'))) return false;
            }

            return true;
        }

        public static string? ExtractTitleId(string romPath)
        {
            var extractor = new GameIdExtractor();

            return extractor.ExtractGameId(romPath);
        }
    }
}