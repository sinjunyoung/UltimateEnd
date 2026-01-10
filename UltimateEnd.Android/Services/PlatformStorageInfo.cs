using Android.Content;
using Android.OS;
using Android.OS.Storage;
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
                var storageManager = (StorageManager)_context.GetSystemService(Context.StorageService);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                {
                    foreach (var volume in storageManager.StorageVolumes)
                    {
                        if (volume.IsRemovable && volume.State == global::Android.OS.Environment.MediaMounted)
                            return volume.Directory?.AbsolutePath;
                    }
                }
                else
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
            }
            catch { }

            return null;
        }
    }
}