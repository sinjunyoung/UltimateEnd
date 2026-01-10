using System.Collections.Generic;
using System.IO;

namespace UltimateEnd.Desktop.Services
{
    public class FolderScanner : UltimateEnd.Services.IFolderScanner
    {
        public List<UltimateEnd.Models.FolderInfo> GetSubfolders(string path)
        {
            var result = new List<UltimateEnd.Models.FolderInfo>();

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return result;

            var dirs = Directory.GetDirectories(path);

            foreach (var dir in dirs)
            {
                result.Add(new UltimateEnd.Models.FolderInfo
                {
                    Name = Path.GetFileName(dir),
                    Path = dir
                });
            }

            return result;
        }
    }
}