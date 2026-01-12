using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.Common.Keys;
using UltimateEnd.SaveFile.Parsers;

namespace UltimateEnd.SaveFile.Switch
{
    public class SwitchFormatParserRegistry
    {
        private readonly List<IFormatParser> _parsers = [];

        public SwitchFormatParserRegistry(KeySet keySet)
        {
            RegisterParser(new NspParser(keySet));
            RegisterParser(new XciParser(keySet));
        }

        public void RegisterParser(IFormatParser parser) => _parsers.Add(parser);

        public IFormatParser? GetParser(string extension) => _parsers.FirstOrDefault(p => p.CanParse(extension));

        public string? ParseGameId(string filePath)
        {
            string ext = Path.GetExtension(filePath);
            var parser = GetParser(ext);

            return parser?.ParseGameId(filePath);
        }
    }
}