using Android.Content;
using Android.Content.PM;
using System;

namespace UltimateEnd.Android.Services
{
    public class AppValidator(Context context)
    {
        private readonly Context _context = context ?? throw new ArgumentNullException(nameof(context));

        public static string? ExtractPackageName(string launchCommand)
        {
            if (string.IsNullOrWhiteSpace(launchCommand))
                return null;

            var parts = launchCommand.Split([' ', '\n', '\r', '\t'],
                                      StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i] == "-n")
                {
                    var component = parts[i + 1];
                    var slashIndex = component.IndexOf('/');
                    return slashIndex > 0 ? component.Substring(0, slashIndex) : component;
                }
            }
            return null;
        }

        public bool IsAppInstalled(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return false;

            try
            {
                var packageManager = _context.PackageManager;
                if (packageManager == null)
                    return false;

                packageManager.GetPackageInfo(packageName, PackageInfoFlags.Activities);

                return true;
            }
            catch (PackageManager.NameNotFoundException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public PackageInfo? GetPackageInfo(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return null;

            try
            {
                var packageManager = _context.PackageManager;

                return packageManager?.GetPackageInfo(packageName, PackageInfoFlags.Activities);
            }
            catch
            {
                return null;
            }
        }
    }
}