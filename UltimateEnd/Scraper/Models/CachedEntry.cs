using System;

namespace UltimateEnd.Scraper.Models
{
    public class CachedEntry
    {
        public int GameId { get; set; }

        public int SystemId { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string ReleaseDate { get; set; }

        public string Developer { get; set; }

        public string Publisher { get; set; }

        public string Genre { get; set; }

        public string Players { get; set; }

        public float Rating { get; set; }

        public string BoxFrontMedia { get; set; }

        public string LogoMedia { get; set; }

        public string VideoMedia { get; set; }

        public string BoxFrontFormat { get; set; }

        public string LogoFormat { get; set; }

        public string VideoFormat { get; set; }

        public DateTime CachedAt { get; set; }
    }
}