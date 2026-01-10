using System.Collections.Generic;

namespace UltimateEnd.Services
{
    public class PlatformMappingConfig
    {
        public Dictionary<string, string> FolderMappings { get; set; } = [];

        public Dictionary<string, string> CustomDisplayNames { get; set; } = [];
    }
}