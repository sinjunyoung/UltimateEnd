using Android.OS;
using System.IO;
using System.Linq;

namespace UltimateEnd.Android.Services
{
    public class StoragePathProvider : UltimateEnd.Services.IStoragePathProvider
    {
        public string? GetDefaultRomsPath()
        {
            var internalStorage = Environment.ExternalStorageDirectory?.AbsolutePath;

            if (internalStorage != null && Directory.Exists(internalStorage))
            {
                var dirs = Directory.GetDirectories(internalStorage);
                var romsDir = dirs.FirstOrDefault(d => Path.GetFileName(d)?.ToLower() == "roms");

                if (romsDir != null)
                    return romsDir;
            }

            var externalDirs = global::Android.App.Application.Context?.GetExternalFilesDirs(null);

            if (externalDirs != null)
            {
                foreach (var dir in externalDirs)
                {
                    if (dir != null)
                    {
                        var path = dir.AbsolutePath;
                        var parts = path.Split('/');

                        if (parts.Length > 2 && parts[1] == "storage")
                        {
                            var sdRoot = $"/storage/{parts[2]}";

                            if (Directory.Exists(sdRoot))
                            {
                                var sdDirs = Directory.GetDirectories(sdRoot);
                                var sdRomsDir = sdDirs.FirstOrDefault(d => Path.GetFileName(d)?.ToLower() == "roms");

                                if (sdRomsDir != null)
                                    return sdRomsDir;
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}