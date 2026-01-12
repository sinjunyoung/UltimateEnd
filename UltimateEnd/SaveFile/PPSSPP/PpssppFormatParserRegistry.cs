using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimateEnd.SaveFile.Parsers;

namespace UltimateEnd.SaveFile.PPSSPP
{
    public class PpssppFormatParserRegistry
    {
        private readonly List<IFormatParser> _parsers = [];

        public PpssppFormatParserRegistry()
        {
            RegisterParser(new PspIsoParser());
            RegisterParser(new PspCsoParser());
            RegisterParser(new PspChdParser());
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