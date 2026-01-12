using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimateEnd.SaveFile.Parsers;

namespace UltimateEnd.SaveFile.Dolphin
{
    public class DolphinFormatParserRegistry
    {
        private readonly List<IFormatParser> _parsers = [];

        public DolphinFormatParserRegistry()
        {
            RegisterParser(new GczParser());
            RegisterParser(new RvzWiaParser());
            RegisterParser(new PlainIsoParser());
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