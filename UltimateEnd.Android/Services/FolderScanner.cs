using AndroidX.DocumentFile.Provider;
using System.Collections.Generic;
namespace UltimateEnd.Android.Services
{
    public class FolderScanner : UltimateEnd.Services.IFolderScanner
    {
        public List<UltimateEnd.Models.FolderInfo> GetSubfolders(string path)
        {
            var result = new List<UltimateEnd.Models.FolderInfo>();
            try
            {
                if (path.StartsWith("content://"))
                    return GetSubfoldersFromSAF(path);
                var javaFile = new Java.IO.File(path);
                if (!javaFile.Exists() || !javaFile.IsDirectory || !javaFile.CanRead())
                    return result;
                var files = javaFile.ListFiles();
                if (files == null)
                    return result;

                foreach (var file in files)
                {
                    if (file != null && file.IsDirectory)
                    {
                        result.Add(new UltimateEnd.Models.FolderInfo
                        {
                            Name = file.Name,
                            Path = file.Name
                        });
                    }
                }
            }
            catch { }
            return result;
        }
        private List<UltimateEnd.Models.FolderInfo> GetSubfoldersFromSAF(string contentUri)
        {
            var result = new List<UltimateEnd.Models.FolderInfo>();
            try
            {
                var context = global::Android.App.Application.Context;
                var uri = global::Android.Net.Uri.Parse(contentUri);
                var rootDir = DocumentFile.FromTreeUri(context, uri);
                if (rootDir != null && rootDir.IsDirectory)
                {
                    var subDirs = rootDir.ListFiles();
                    if (subDirs != null)
                    {
                        // PathConverter 제거!
                        foreach (var dir in subDirs)
                        {
                            if (dir != null && dir.IsDirectory && dir.Name != null)
                            {
                                result.Add(new UltimateEnd.Models.FolderInfo
                                {
                                    Name = dir.Name,
                                    Path = dir.Name
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            return result;
        }
    }
}