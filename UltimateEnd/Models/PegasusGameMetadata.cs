using System;
using System.Collections.Generic;
using UltimateEnd.Enums;

namespace UltimateEnd.Models
{
    public class PegasusGameMetadata
    {
        public string Title { get; set; }

        public List<string> Files { get; set; } = [];

        public List<string> Developers { get; set; } = [];

        public List<string> Publishers { get; set; } = [];

        public List<string> Genres { get; set; } = [];

        public List<string> Tags { get; set; } = [];

        public int? PlayerCount { get; set; }

        public string Summary { get; set; }

        public string Description { get; set; }

        public DateTime? ReleaseDate { get; set; }

        public float? Rating { get; set; }

        public string LaunchCmd { get; set; }

        public string LaunchWorkdir { get; set; }

        public string SortBy { get; set; }

        public Dictionary<AssetType, List<string>> Assets { get; set; } = [];

        public Dictionary<string, List<string>> ExtraFields { get; set; } = [];
    }
}