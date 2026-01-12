using System.IO;
using LibHac.Common.Keys;
using UltimateEnd.Models;

namespace UltimateEnd.SaveFile.Switch
{
    public class GameIdExtractor : IGameIdExtractor
    {
        private static KeySet? _keySetCache;
        private static string? _keysPath;
        private SwitchFormatParserRegistry? _parserRegistry;

        public static void SetKeysPath(string keysPath)
        {
            _keysPath = keysPath;
            _keySetCache = null;
        }

        private static KeySet LoadKeySet()
        {
            if (_keySetCache != null) return _keySetCache;

            var keySet = new KeySet();

            if (!string.IsNullOrEmpty(_keysPath) && File.Exists(_keysPath))
            {
                try
                {
                    ExternalKeyReader.ReadKeyFile(keySet, filename: _keysPath);
                    _keySetCache = keySet;
                }
                catch
                {
                }
            }

            return keySet;
        }

        private SwitchFormatParserRegistry GetRegistry()
        {
            if (_parserRegistry == null)
            {
                var keySet = LoadKeySet();
                _parserRegistry = new SwitchFormatParserRegistry(keySet);
            }

            return _parserRegistry;
        }

        public string? ExtractGameId(string romPath)
        {
            if (string.IsNullOrEmpty(romPath) || !File.Exists(romPath)) return null;

            var gameId = GetRegistry().ParseGameId(romPath);

            return IsValidGameId(gameId) ? gameId : null;
        }

        public bool IsValidGameId(string? gameId)
        {
            if (string.IsNullOrEmpty(gameId) || gameId.Length != 16) return false;

            foreach (char c in gameId)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
            }

            return true;
        }

        public static string? ExtractGameId(GameMetadata game)
        {
            var extractor = new GameIdExtractor();

            return extractor.ExtractGameId(game?.GetRomFullPath());
        }
    }
}