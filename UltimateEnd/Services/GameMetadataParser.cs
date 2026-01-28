using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public static class GameMetadataParser
    {
        private static readonly Lock _writeLock = new();

        private const int InitialCapacity = 100;
        private const int StringBuilderCapacity = 256;

        public static List<GameMetadata> Parse(string filePath, string basePath)
        {
            if (!File.Exists(filePath))
                return [];

            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is IOException)
            {
                return [];
            }

            var result = new List<GameMetadata>(InitialCapacity);
            GameMetadata currentGame = null;
            string currentKey = null;
            var currentValue = new StringBuilder(StringBuilderCapacity);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                int start = 0;
                int end = line.Length - 1;

                while (start <= end && char.IsWhiteSpace(line[start])) start++;
                while (end >= start && char.IsWhiteSpace(line[end])) end--;

                int length = end - start + 1;

                if (length == 0 || line[start] == '#')
                    continue;

                if (line[start] == '[' && line[end] == ']')
                {
                    SaveCurrentKeyValue(ref currentGame, ref currentKey, currentValue);

                    int secStart = start + 1;
                    int secEnd = end - 1;

                    while (secStart <= secEnd && char.IsWhiteSpace(line[secStart])) secStart++;
                    while (secEnd >= secStart && char.IsWhiteSpace(line[secEnd])) secEnd--;

                    if (secEnd - secStart + 1 == 15 &&
                        IsDefaultSettings(line, secStart, secEnd))
                    {
                        currentGame = null;

                        continue;
                    }

                    if (currentGame != null && !string.IsNullOrEmpty(currentGame.RomFile))
                    {
                        currentGame.SetBasePath(basePath);

                        if (File.Exists(currentGame.GetRomFullPath()))
                            result.Add(currentGame);
                    }

                    currentGame = new GameMetadata();

                    continue;
                }

                if (currentGame == null)
                    continue;

                int equalIndex = -1;

                for (int j = start; j <= end; j++)
                {
                    if (line[j] == '=')
                    {
                        equalIndex = j;
                        break;
                    }
                }

                if (equalIndex > start)
                {
                    int keyEnd = equalIndex - 1;

                    while (keyEnd >= start && char.IsWhiteSpace(line[keyEnd])) keyEnd--;

                    char firstChar = line[start];

                    if (IsLetter(firstChar) || firstChar == '_')
                    {
                        SaveCurrentKeyValue(ref currentGame, ref currentKey, currentValue);

                        currentKey = line.Substring(start, keyEnd - start + 1);

                        int valStart = equalIndex + 1;

                        while (valStart <= end && char.IsWhiteSpace(line[valStart])) valStart++;

                        if (valStart <= end)
                            currentValue.Append(line, valStart, end - valStart + 1);

                        continue;
                    }
                }

                if (currentKey != null && IsDescription(currentKey))
                {
                    if (currentValue.Length > 0)
                        currentValue.Append('\n');
                    currentValue.Append(line, start, length);
                }
            }

            SaveCurrentKeyValue(ref currentGame, ref currentKey, currentValue);
            if (currentGame != null && !string.IsNullOrEmpty(currentGame.RomFile))
            {
                currentGame.SetBasePath(basePath);

                if (File.Exists(currentGame.GetRomFullPath()))
                    result.Add(currentGame);
            }

            return result;
        }

        private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private static bool IsDefaultSettings(string line, int start, int end)
        {
            ReadOnlySpan<char> span = line.AsSpan(start, end - start + 1);

            return span.Equals("DefaultSettings".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDescription(string key)
        {
            if (key.Length != 11) return false;

            return key[0] == 'd' || key[0] == 'D';
        }

        private static void SaveCurrentKeyValue(ref GameMetadata currentGame, ref string currentKey, StringBuilder currentValue)
        {
            if (currentGame == null || currentKey == null)
                return;

            var value = IsDescription(currentKey) ? NormalizeWhitespace(currentValue) : TrimStringBuilder(currentValue);

            SetGameProperty(currentGame, currentKey, value);
            currentKey = null;
            currentValue.Clear();
        }

        private static string TrimStringBuilder(StringBuilder sb)
        {
            if (sb.Length == 0) return string.Empty;

            int start = 0;
            int end = sb.Length - 1;

            while (start <= end && char.IsWhiteSpace(sb[start])) start++;
            while (end >= start && char.IsWhiteSpace(sb[end])) end--;

            return sb.ToString(start, end - start + 1);
        }

        private static string NormalizeWhitespace(StringBuilder sb)
        {
            if (sb.Length == 0) return string.Empty;

            var result = new StringBuilder(sb.Length);
            bool lastWasNewline = true;

            for (int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];

                if (c == '\n')
                {
                    if (!lastWasNewline)
                    {
                        result.Append('\n');
                        lastWasNewline = true;
                    }
                }
                else if (c == '\r')
                {
                    continue;
                }
                else if (!char.IsWhiteSpace(c))
                {
                    int lineStart = i;

                    while (i < sb.Length && sb[i] != '\n' && sb[i] != '\r')
                        i++;

                    int lineEnd = i - 1;

                    while (lineEnd >= lineStart && char.IsWhiteSpace(sb[lineEnd]))
                        lineEnd--;

                    if (lineEnd >= lineStart)
                    {
                        for (int j = lineStart; j <= lineEnd; j++)
                            result.Append(sb[j]);

                        lastWasNewline = false;
                    }

                    i--;
                }
            }

            return result.ToString();
        }

        private static void SetGameProperty(GameMetadata game, string key, string value)
        {
            if (string.IsNullOrEmpty(value) && key[0] != 'r') return;

            switch (key[0])
            {
                case 'r':
                case 'R': // romfile
                    if (key.Length == 7) game.RomFile = value;
                    break;
                case 't':
                case 'T': // title
                    if (key.Length == 5) game.Title = value;
                    break;
                case 's':
                case 'S': // scrapHint, subFolder
                    if (key.Length == 9)
                    {
                        if (key[1] == 'c' || key[1] == 'C') // scrapHint
                            game.ScrapHint = value;
                        else if (key[1] == 'u' || key[1] == 'U') // subFolder
                            game.SubFolder = value;
                    }
                    break;
                case 'e':
                case 'E': // emulater id
                    if (key.Length == 10) game.EmulatorId = value;
                    break;
                case 'd':
                case 'D': // description, developer
                    if (key.Length == 11) game.Description = value;
                    else if (key.Length == 9) game.Developer = value;
                    break;
                case 'g':
                case 'G': // genre
                    if (key.Length == 5) game.Genre = value;
                    break;
                case 'h':
                case 'H': // hasKorean
                    if (key.Length == 9)
                        game.HasKorean = value.Length == 4 && (value[0] == 't' || value[0] == 'T');
                    break;
                case 'i':
                case 'I': // isFavorite, ignore
                    if (key.Length == 10)
                        game.IsFavorite = value.Length == 4 && (value[0] == 't' || value[0] == 'T');
                    else if (key.Length == 6)
                        game.Ignore = value.Length == 4 && (value[0] == 't' || value[0] == 'T');
                    break;
                case 'c':
                case 'C': // coverImagePath
                    if (key.Length == 14) game.CoverImagePath = value;
                    break;
                case 'l':
                case 'L': // logoImagePath
                    if (key.Length == 13) game.LogoImagePath = value;
                    break;
                case 'v':
                case 'V': // videoPath
                    if (key.Length == 9) game.VideoPath = value;
                    break;
            }
        }

        public static void Write(string filePath, IEnumerable<GameMetadata> games)
        {
            lock (_writeLock)
            {
                var sb = new StringBuilder(4096);
                sb.Append("# UltimateEnd Game Metadata\n\n");

                foreach (var game in games)
                {
                    var sectionName = !string.IsNullOrEmpty(game.Title) ? game.Title : Path.GetFileNameWithoutExtension(game.RomFile);

                    sb.Append('[').Append(sectionName).Append("]\n");
                    sb.Append("romFile=").Append(game.RomFile).Append('\n');

                    if (!string.IsNullOrEmpty(game.Title))
                        sb.Append("title=").Append(game.Title).Append('\n');

                    if (!string.IsNullOrEmpty(game.ScrapHint))
                        sb.Append("scrapHint=").Append(game.ScrapHint).Append('\n');

                    if (!string.IsNullOrEmpty(game.EmulatorId))
                        sb.Append("emulatorId=").Append(game.EmulatorId).Append('\n');

                    if (!string.IsNullOrEmpty(game.Developer))
                        sb.Append("developer=").Append(game.Developer).Append('\n');

                    if (!string.IsNullOrEmpty(game.Genre))
                        sb.Append("genre=").Append(game.Genre).Append('\n');

                    if (game.HasKorean)
                        sb.Append("hasKorean=true\n");

                    if (game.IsFavorite)
                        sb.Append("isFavorite=true\n");

                    if (game.Ignore)
                        sb.Append("ignore=true\n");

                    if (!string.IsNullOrEmpty(game.CoverImagePath))
                        sb.Append("coverImagePath=").Append(game.CoverImagePath).Append('\n');

                    if (!string.IsNullOrEmpty(game.LogoImagePath))
                        sb.Append("logoImagePath=").Append(game.LogoImagePath).Append('\n');

                    if (!string.IsNullOrEmpty(game.VideoPath))
                        sb.Append("videoPath=").Append(game.VideoPath).Append('\n');

                    if (!string.IsNullOrEmpty(game.Description))
                        sb.Append("description=").Append(game.Description).Append('\n');

                    sb.Append('\n');
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            }
        }
    }
}