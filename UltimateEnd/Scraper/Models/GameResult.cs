using System.Collections.Generic;

namespace UltimateEnd.Scraper.Models
{
    public class GameResult
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string ReleaseDate { get; set; }

        public string Developer { get; set; }

        public string Publisher { get; set; }

        public string Genre { get; set; }

        public string Players { get; set; }

        public float Rating { get; set; }

        public int MatchScore { get; set; }

        public Dictionary<string, MediaInfo> Media { get; set; } = [];
    }
}