using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UltimateEnd.Services
{
    public static class ConfigFileParser
    {
        public static Dictionary<string, Dictionary<string, string>> Parse(string filePath)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            if (!File.Exists(filePath))
                return result;

            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            string currentSection = null;
            string currentKey = null;
            var currentValue = new StringBuilder();

            void SaveCurrentKeyValue()
            {
                if (currentSection != null && currentKey != null)
                {
                    var value = currentValue.ToString().Trim();
                    value = NormalizeWhitespace(value);
                    result[currentSection][currentKey] = value;
                    currentKey = null;
                    currentValue.Clear();
                }
            }

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.Length == 0 || trimmed[0] == '#')
                    continue;

                if (trimmed[0] == '[' && trimmed[trimmed.Length - 1] == ']')
                {
                    SaveCurrentKeyValue();
                    currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                    if (!result.ContainsKey(currentSection))
                        result[currentSection] = [];
                    continue;
                }

                if (currentSection == null)
                    continue;

                int equalIndex = trimmed.IndexOf('=');
                if (equalIndex > 0)
                {
                    SaveCurrentKeyValue();
                    currentKey = trimmed[..equalIndex].Trim();
                    var value = trimmed[(equalIndex + 1)..].Trim();
                    currentValue.Append(value);
                    continue;
                }

                if (currentKey != null)
                {
                    if (currentValue.Length > 0)
                        currentValue.Append('\n');
                    currentValue.Append(trimmed);
                }
            }

            SaveCurrentKeyValue();

            return result;
        }

        private static string NormalizeWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var result = new StringBuilder(value.Length);
            bool lastWasNewline = false;
            bool firstLine = true;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (c == '\n')
                {
                    if (!lastWasNewline && !firstLine)
                    {
                        result.Append('\n');
                        lastWasNewline = true;
                    }
                }
                else if (c != '\r')
                {
                    result.Append(c);
                    lastWasNewline = false;
                    firstLine = false;
                }
            }

            var lines = result.ToString().Split('\n');
            result.Clear();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length > 0)
                {
                    if (result.Length > 0)
                        result.Append('\n');
                    result.Append(line);
                }
            }

            return result.ToString();
        }

        public static void Write(string filePath, Dictionary<string, Dictionary<string, string>> data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# UltimateEnd Emulator Configuration");
            sb.AppendLine("# Lines starting with # are comments");
            sb.AppendLine();

            foreach (var section in data)
            {
                sb.Append('[');
                sb.Append(section.Key);
                sb.AppendLine("]");

                foreach (var kvp in section.Value)
                {
                    sb.Append(kvp.Key);
                    sb.Append('=');
                    sb.AppendLine(kvp.Value);
                }

                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString());
        }
    }
}