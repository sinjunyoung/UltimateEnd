using System;
using System.Collections.Generic;

namespace UltimateEnd.Scraper.Models
{
    public class BatchScrapResult
    {
        public int TotalCount { get; set; }

        public int SuccessCount { get; set; }

        public int FailedCount { get; set; }

        public int SkippedCount { get; set; }

        public int CachedCount { get; set; }

        public TimeSpan TotalElapsed { get; set; }

        public List<(string romPath, SearchResult result)> Results { get; set; } = [];

        public List<(string romPath, string error)> Failures { get; set; } = [];
    }
}
