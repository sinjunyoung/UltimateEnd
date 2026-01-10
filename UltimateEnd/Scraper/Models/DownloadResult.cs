using System;
using System.Collections.Generic;

namespace UltimateEnd.Scraper.Models
{
    public class DownloadResult
    {
        public List<string> Success { get; set; } = [];

        public int TotalCount { get; set; }

        public TimeSpan ElapsedTime { get; set; }

        public List<string> Errors { get; set; } = [];

        public bool IsSuccess => Success.Count == TotalCount;
    }
}