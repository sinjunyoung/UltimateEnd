using Avalonia.Platform.Storage;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop
{
    public class PathResolver : IPathResolver
    {
        public string GetPath(IStorageFile file) => file.Path.LocalPath;
    }
}