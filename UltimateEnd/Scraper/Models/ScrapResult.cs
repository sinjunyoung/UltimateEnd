using System;
using System.Collections.Generic;
using UltimateEnd.Enums;

namespace UltimateEnd.Scraper.Models
{
    public class ScrapResult
    {
        public ScrapResultType ResultType { get; set; }

        public bool IsSuccess => ResultType == ScrapResultType.Success || ResultType == ScrapResultType.Cached;

        public string Message { get; set; }

        public bool MetadataUpdated { get; set; }

        public int MediaDownloaded { get; set; }

        public List<string> Warnings { get; set; } = [];

        public TimeSpan Elapsed { get; set; }
    }
}