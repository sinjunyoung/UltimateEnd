using UltimateEnd.Enums;

namespace UltimateEnd.Scraper.Models
{
    public class FetchResult
    {
        public ScrapResultType ResultType { get; set; }

        public string Message { get; set; }

        public GameResult Game { get; set; }
    }
}