using Android.Content;
using Avalonia.Platform.Storage;
using UltimateEnd.Services;

namespace UltimateEnd.Android
{
    public class PathResolver : IPathResolver
    {
        private readonly IPathConverter _pathConverter;

        public PathResolver(Context context)
        {
            _pathConverter = new Services.PathConverter(context);
        }

        public string GetPath(IStorageFile file)
        {
            var uriString = file.Path.AbsoluteUri;

            if (uriString.StartsWith("content://"))
                return _pathConverter.UriToFriendlyPath(uriString);

            return file.Path.LocalPath;
        }
    }
}