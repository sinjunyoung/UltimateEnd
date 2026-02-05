using System;

namespace UltimateEnd.Extractor
{
    public class CachedMetadata
    {
        public string TitleId { get; set; }

        public string Title { get; set; }

        public string Developer { get; set; }

        public string Description { get; set; }

        public string CoverImagePath { get; set; }

        public string LogoImagePath { get; set; }

        public long FileSize { get; set; }

        public DateTime LastModified { get; set; }
    }
}