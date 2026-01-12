using System.IO;

namespace UltimateEnd.SaveFile.Parsers
{
    public class PspIsoParser : IFormatParser
    {
        public bool CanParse(string extension) => extension.Equals(".iso", System.StringComparison.CurrentCultureIgnoreCase);

        public string? ParseGameId(string filePath)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                var paramSfoData = ParamSfoParser.FindInStream(stream);

                return paramSfoData != null ? ParamSfoParser.ParseDiscId(paramSfoData) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}