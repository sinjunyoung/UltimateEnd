using System;
using System.Collections.Generic;

namespace UltimateEnd.Scraper.Models
{
    public class SearchResult
    {
        public List<GameResult> Games { get; set; } = [];

        public int TotalFound { get; set; }

        public TimeSpan ElapsedTime { get; set; }

        public string ErrorMessage { get; set; }

        public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
    }
}