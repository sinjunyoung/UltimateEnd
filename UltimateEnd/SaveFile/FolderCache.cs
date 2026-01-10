using System.Collections.Generic;

namespace UltimateEnd.SaveFile
{
    public class FolderCache
    {
        public string UltimateEndFolderId { get; set; }

        public string RetroArchFolderId { get; set; }

        public string SavesFolderId { get; set; }

        public Dictionary<string, string> CoreFolders { get; set; }
    }
}