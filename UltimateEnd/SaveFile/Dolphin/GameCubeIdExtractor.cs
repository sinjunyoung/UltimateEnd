using System;
using System.IO;

namespace UltimateEnd.SaveFile.Dolphin
{
    public class GameCubeIdExtractor : IGameIdExtractor
    {
        private readonly DolphinFormatParserRegistry _parserRegistry = new();

        public string? ExtractGameId(string romPath)
        {
            if (!File.Exists(romPath)) return null;

            var gameId = _parserRegistry.ParseGameId(romPath);

            return IsValidGameId(gameId) ? gameId : null;
        }

        public bool IsValidGameId(string? gameId)
        {
            if (string.IsNullOrWhiteSpace(gameId) || gameId.Length < 4) return false;

            char firstChar = gameId[0];

            return firstChar == 'G' || firstChar == 'D' || firstChar == 'P' ||
                   firstChar == 'R' || firstChar == 'S';
        }

        public static string? ExtractGameIdFromGci(string gciFilePath)
        {
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(gciFilePath);
                var parts = fileName.Split('-');

                if (parts.Length >= 2)
                {
                    string gameId = parts[1].Trim();

                    if (gameId.Length >= 4 && gameId.Length <= 6) return gameId;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static string[] FindGciFiles(string dolphinBasePath, string gameId)
        {
            var results = new System.Collections.Generic.List<string>();
            string gcPath = Path.Combine(dolphinBasePath, "GC");

            if (!Directory.Exists(gcPath)) return [.. results];

            string[] regions = ["USA", "EUR", "JAP"];
            string[] cards = ["Card A", "Card B"];
            string searchPattern = gameId.Length >= 4 ? gameId[..4] : gameId;

            foreach (var region in regions)
            {
                foreach (var card in cards)
                {
                    var cardPath = Path.Combine(gcPath, region, card);

                    if (!Directory.Exists(cardPath)) continue;

                    var gciFiles = Directory.GetFiles(cardPath, "*.gci");

                    foreach (var gciFile in gciFiles)
                    {
                        var extractedId = ExtractGameIdFromGci(gciFile);

                        if (extractedId != null && extractedId.StartsWith(searchPattern, StringComparison.OrdinalIgnoreCase)) results.Add(gciFile);
                    }
                }
            }
            return [.. results];
        }

        public static string GetRegion(string gameId)
        {
            if (gameId.Length < 4) return "USA";

            char regionChar = gameId[3];
            return regionChar switch
            {
                'E' => "USA",
                'P' => "EUR",
                'J' => "JAP",
                _ => "USA"
            };
        }
    }
}