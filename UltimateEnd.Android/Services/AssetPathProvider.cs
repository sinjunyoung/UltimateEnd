using System;
using System.Diagnostics;
using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class AssetPathProvider : IAssetPathProvider
    {
        private readonly string _baseDir;
        private readonly string _ultimateEndFolder;

        public AssetPathProvider()
        {
            var settingsFolder = new AppBaseFolderProvider().GetSettingsFolder();
            _ultimateEndFolder = Directory.GetParent(settingsFolder).FullName;
            _baseDir = Path.Combine(_ultimateEndFolder, "Assets");

            EnsureDirectoryCreated(Path.Combine(_baseDir, "Sounds"));
            EnsureDirectoryCreated(Path.Combine(_baseDir, "DBs"));
            EnsureDirectoryCreated(Path.Combine(_ultimateEndFolder, "Themes"));

            CopyAssetsOnce();
        }

        private static void EnsureDirectoryCreated(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private void CopyAssetsOnce()
        {
            CopyAssetFolder("Themes", Path.Combine(_ultimateEndFolder, "Themes"));
            CopyAssetFolder("Sounds", Path.Combine(_baseDir, "Sounds"));
            CopyAssetFolder("DBs", Path.Combine(_baseDir, "DBs"));            
        }

        private void CopyAssetFolder(string assetPath, string targetDir)
        {
            var context = global::Android.App.Application.Context;

            try
            {
                var files = context.Assets?.List(assetPath);
                if (files == null || files.Length == 0) return;

                foreach (var fileName in files)
                {
                    var destPath = Path.Combine(targetDir, fileName);
                    if (File.Exists(destPath)) continue;

                    using var assetStream = context.Assets!.Open($"{assetPath}/{fileName}");
                    using var fileStream = File.Create(destPath);
                    assetStream.CopyTo(fileStream);
                    fileStream.Flush();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Asset copy failed: {ex.Message}");
            }
        }

        public string GetAssetPath(string subFolder, string fileName) => Path.Combine(_baseDir, subFolder, fileName);
    }
}