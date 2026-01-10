using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class PlatformService : IPlatformService
    {
        public string GetAppVersion()
        {
            var context = global::Android.App.Application.Context;
            var packageInfo = context.PackageManager?.GetPackageInfo(context.PackageName, 0);

            return packageInfo?.VersionName ?? "0.0.0";
        }
    }
}