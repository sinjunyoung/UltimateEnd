namespace UltimateEnd.Utils
{
    public static class UriHelper
    {
        private const string SteamUriScheme = "steam://";

        public static bool IsSteamUri(string uri)
        {
            return !string.IsNullOrEmpty(uri) && uri.StartsWith(SteamUriScheme, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUriScheme(string uri)
        {
            return !string.IsNullOrEmpty(uri) && uri.Contains("://");
        }
    }
}