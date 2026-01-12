using System;
using System.IO;
using System.Linq;
using UltimateEnd.Models;

namespace UltimateEnd.SaveFile.PPSSPP
{
    public class GameIdExtractor : IGameIdExtractor
    {
        private readonly PpssppFormatParserRegistry _parserRegistry = new();

        public string? ExtractGameId(string romPath)
        {
            if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath)) return null;

            var gameId = _parserRegistry.ParseGameId(romPath);

            return IsValidGameId(gameId) ? gameId : null;
        }

        public bool IsValidGameId(string? id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != 9) return false;

            var prefix = id[..4].ToUpper();
            var number = id[4..];

            var validPrefixes = new[]
            {
                "ULUS", "UCUS", "NPUZ", "NPUX", "NPUF", "NPUH", "NPUG",
                "ULES", "UCES", "NPEZ", "NPEX", "NPEH", "NPEG",
                "ULJS", "ULJM", "UCJS", "UCJM", "UCJB", "NPJJ", "NPJH", "NPJG",
                "ULKS", "UCKS", "NPHH", "NPHG",
                "ULAS", "UCAS", "NPHZ"
            };

            return validPrefixes.Contains(prefix) && number.All(char.IsDigit);
        }

        public static string? GetGameId(GameMetadata game)
        {
            var extractor = new GameIdExtractor();

            return extractor.ExtractGameId(game.GetRomFullPath());
        }
    }
}