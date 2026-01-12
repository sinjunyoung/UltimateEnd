namespace UltimateEnd.SaveFile.Parsers
{
    public class PlainIsoParser : IFormatParser
    {
        public bool CanParse(string extension) => extension.Equals(".iso", System.StringComparison.CurrentCultureIgnoreCase);

        public string? ParseGameId(string filePath) => FileFormatUtils.ReadGameIdFromStart(filePath, 6);
    }
}