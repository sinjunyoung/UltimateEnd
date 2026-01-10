using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class PathConverter : IPathConverter
    {
        public string UriToFriendlyPath(string path) => path;

        public string FriendlyPathToUri(string path) => path;

        public string FriendlyPathToRealPath(string path) => path;

        public string RealPathToFriendlyPath(string path) => path;
    }
}