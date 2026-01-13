using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class PlatformService(global::Android.Content.Context context) : IPlatformService
    {
        public string GetAppVersion()
        {
            var context = global::Android.App.Application.Context;
            var packageInfo = context.PackageManager?.GetPackageInfo(context.PackageName, 0);

            return packageInfo?.VersionName ?? "0.0.0";
        }

        public string GetAppName()
        {
            var appInfo = context.ApplicationInfo;
            var label = context.PackageManager?.GetApplicationLabel(appInfo);
            if (label != null)
                return label.ToString();

            return "UltimateEnd";
        }
    }
}