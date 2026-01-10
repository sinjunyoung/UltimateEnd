namespace UltimateEnd.Models
{
    public class TokenContext
    {
        public string RomPath { get; set; } = string.Empty;

        public string RomDir { get; set; } = string.Empty;

        public string RomName { get; set; } = string.Empty;

        public string? CoreName { get; set; }

        public string? CorePath { get; set; }

        public string? FileUriRomPath { get; set; }

        public string? SafUriRomPath { get; set; }
    }
}