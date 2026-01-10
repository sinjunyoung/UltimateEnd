using System.Collections.Generic;
using System.Text;

namespace UltimateEnd.Desktop.Utils
{
    public static class CommandParser
    {
        public static (string executable, string arguments) ParseCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return (string.Empty, string.Empty);

            command = command.Trim();

            if (command.StartsWith("\""))
            {
                var endQuoteIndex = command.IndexOf("\"", 1);

                if (endQuoteIndex > 0)
                {
                    var executable = command.Substring(1, endQuoteIndex - 1);
                    var arguments = command.Length > endQuoteIndex + 1
                        ? command.Substring(endQuoteIndex + 1).Trim()
                        : string.Empty;
                    return (executable, arguments);
                }
            }

            var firstSpaceIndex = command.IndexOf(' ');

            if (firstSpaceIndex > 0)
            {
                var executable = command.Substring(0, firstSpaceIndex);
                var arguments = command.Substring(firstSpaceIndex + 1).Trim();
                return (executable, arguments);
            }

            return (command, string.Empty);
        }

        public static string ExtractExecutable(string command)
        {
            var (executable, _) = ParseCommand(command);
            return executable;
        }

        public static string JoinArguments(params string[] args)
        {
            var result = new StringBuilder();

            foreach (var arg in args)
            {
                if (string.IsNullOrEmpty(arg))
                    continue;

                if (result.Length > 0)
                    result.Append(' ');

                result.Append(WrapInQuotesIfNeeded(arg));
            }

            return result.ToString();
        }

        private static string WrapInQuotesIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.StartsWith("\"") && value.EndsWith("\""))
                return value;

            if (value.Contains(' ') || value.Contains('\t'))
                return $"\"{value}\"";

            return value;
        }

        public static List<string> SplitCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
                return new List<string>();

            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            bool escapeNext = false;

            foreach (char c in commandLine)
            {
                if (escapeNext)
                {
                    current.Append(c);
                    escapeNext = false;
                    continue;
                }

                if (c == '\\')
                {
                    escapeNext = true;
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                    current.Append(c);
            }

            if (current.Length > 0)
                result.Add(current.ToString());

            return result;
        }
    }
}