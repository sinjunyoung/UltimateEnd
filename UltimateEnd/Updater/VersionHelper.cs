using System.Linq;
using UltimateEnd.Services;

namespace UltimateEnd.Updater
{
    public static class VersionHelper
    {
        public static string GetCurrentVersion()
        {
            var ver = PlatformServiceFactory.Create?.Invoke();
            
            return $"v{ver.GetAppVersion()}";
        }

        public static bool IsNewerVersion(string current, string latest)
        {
            current = current?.TrimStart('v') ?? "0.0.0";
            latest = latest?.TrimStart('v') ?? "0.0.0";

            var currentParts = current.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();
            var latestParts = latest.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).ToArray();

            var currentVersion = new int[]
            {
                currentParts.Length > 0 ? currentParts[0] : 0,
                currentParts.Length > 1 ? currentParts[1] : 0,
                currentParts.Length > 2 ? currentParts[2] : 0
            };

            var latestVersion = new int[]
            {
                latestParts.Length > 0 ? latestParts[0] : 0,
                latestParts.Length > 1 ? latestParts[1] : 0,
                latestParts.Length > 2 ? latestParts[2] : 0
            };

            for (int i = 0; i < 3; i++)
            {
                if (latestVersion[i] > currentVersion[i]) return true;
                if (latestVersion[i] < currentVersion[i]) return false;
            }

            return false;
        }
    }
}