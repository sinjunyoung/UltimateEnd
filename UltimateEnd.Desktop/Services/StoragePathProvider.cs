using System.IO;
using System.Linq;

namespace UltimateEnd.Desktop.Services
{
    public class StoragePathProvider : UltimateEnd.Services.IStoragePathProvider
    {
        public string? GetDefaultRomsPath()
        {
            var docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

            if (Directory.Exists(docs))
            {
                var dirs = Directory.GetDirectories(docs);
                var romsDir = dirs.FirstOrDefault(d => Path.GetFileName(d)?.ToLower() == "roms");

                if (romsDir != null)
                    return romsDir;
            }

            return null;
        }
    }
}