using System.Collections.Generic;
using UltimateEnd.Enums;

namespace UltimateEnd.Models
{
    public class PegasusCollectionMetadata
    {
        public string Name { get; set; }

        public string ShortName { get; set; }

        public string Summary { get; set; }

        public string Description { get; set; }

        public string LaunchCmd { get; set; }

        public string LaunchWorkdir { get; set; }

        public string SortBy { get; set; }

        public List<string> Directories { get; set; } = [];

        public List<string> Extensions { get; set; } = [];

        public List<string> Files { get; set; } = [];

        public List<string> IgnoreExtensions { get; set; } = [];

        public List<string> IgnoreFiles { get; set; } = [];

        public string Regex { get; set; }

        public string IgnoreRegex { get; set; }

        public Dictionary<AssetType, List<string>> Assets { get; set; } = [];

        public Dictionary<string, List<string>> ExtraFields { get; set; } = [];
    }
}