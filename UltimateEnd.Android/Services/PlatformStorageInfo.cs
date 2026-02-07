using Android.Content;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class PlatformStorageInfo(Context context) : IPlatformStorageInfo
    {
        private readonly Context _context = context;

        public string? GetPrimaryStoragePath() => global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0";

        public string? GetExternalSdCardPath()
        {
            try
            {
                var externalDirs = _context.GetExternalFilesDirs(null);

                if (externalDirs?.Length > 1)
                {
                    var sdCardPath = externalDirs[1]?.AbsolutePath;

                    if (!string.IsNullOrEmpty(sdCardPath))
                    {
                        var parts = sdCardPath.Split('/');

                        if (parts.Length > 2 && parts[1] == "storage")
                            return $"/storage/{parts[2]}";
                    }
                }
            }
            catch { }

            return null;
        }
    }
}