using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimateEnd.Android.Models;
using ActivityInfo = UltimateEnd.Android.Models.ActivityInfo;

namespace UltimateEnd.Android.Services
{
    public class InstalledAppsService
    {
        #region Public Methods - App List

        public List<InstalledAppInfo> GetInstalledApps()
        {
            var apps = new List<InstalledAppInfo>();

            try
            {
                var context = AndroidApplication.AppContext;
                var pm = context.PackageManager;
                if (pm == null) return apps;

                var intent = new Intent(Intent.ActionMain);
                intent.AddCategory(Intent.CategoryLauncher);

                var resolveInfos = pm.QueryIntentActivities(intent, 0);

                foreach (var resolveInfo in resolveInfos)
                {
                    try
                    {
                        var packageName = resolveInfo.ActivityInfo.PackageName;
                        var activityName = resolveInfo.ActivityInfo.Name;
                        var appName = resolveInfo.LoadLabel(pm)?.ToString() ?? packageName;

                        Avalonia.Media.Imaging.Bitmap? avaloniaIcon = null;
                        try
                        {
                            var drawable = resolveInfo.LoadIcon(pm);
                            if (drawable != null)
                                avaloniaIcon = ConvertDrawableToAvaloniaBitmap(drawable, context);
                        }
                        catch { }

                        apps.Add(new InstalledAppInfo
                        {
                            DisplayName = appName!,
                            PackageName = packageName!,
                            ActivityName = activityName!,
                            Icon = avaloniaIcon
                        });
                    }
                    catch { }
                }

                return apps.OrderBy(a => a.DisplayName).ToList();
            }
            catch
            {
                return apps;
            }
        }

        #endregion

        #region Public Methods - App Info

        public (Avalonia.Media.Imaging.Bitmap? Icon, string AppName) GetAppIconAndName(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return (null, string.Empty);

            try
            {
                var context = AndroidApplication.AppContext;
                var pm = context.PackageManager;

                var appInfo = pm.GetApplicationInfo(packageName, 0);
                var appName = pm.GetApplicationLabel(appInfo)?.ToString() ?? packageName;

                var drawable = pm.GetApplicationIcon(packageName);
                if (drawable != null)
                {
                    var icon = ConvertDrawableToAvaloniaBitmap(drawable, context);
                    return (icon, appName);
                }

                return (null, appName);
            }
            catch
            {
                return (null, string.Empty);
            }
        }

        #endregion

        #region Public Methods - Activity List

        public List<ActivityInfo> GetPackageActivities(string packageName)
        {
            var activities = new List<ActivityInfo>();

            try
            {
                var context = AndroidApplication.AppContext;
                var pm = context.PackageManager;
                if (pm == null) return activities;

                var packageInfo = pm.GetPackageInfo(packageName, PackageInfoFlags.Activities);
                if (packageInfo?.Activities == null) return activities;

                foreach (var activity in packageInfo.Activities)
                {
                    if (activity.Exported || activity.Name == packageInfo.Activities[0]?.Name)
                    {
                        var isLauncher = IsLauncherActivity(pm, packageName, activity.Name);
                        var supportsView = SupportsViewAction(pm, packageName, activity.Name);

                        activities.Add(new ActivityInfo
                        {
                            Name = activity.Name,
                            IsLauncher = isLauncher,
                            SupportsView = supportsView
                        });
                    }
                }

                return activities.OrderByDescending(a => a.IsLauncher)
                                .ThenByDescending(a => a.SupportsView)
                                .ToList();
            }
            catch
            {
                return activities;
            }
        }

        public (List<ActivityInfo> Activities, ActivityInfo? Selected) GetPackageActivitiesWithAutoSelect(string packageName)
        {
            var activities = GetPackageActivities(packageName);

            if (activities.Count == 0)
                return (activities, null);

            var viewOnlyActivity = activities.FirstOrDefault(a => a.SupportsView && !a.IsLauncher);
            var mainAndViewActivity = activities.FirstOrDefault(a => a.SupportsView && a.IsLauncher);
            var launcherActivity = activities.FirstOrDefault(a => a.IsLauncher);

            var selectedActivity = viewOnlyActivity
                ?? mainAndViewActivity
                ?? launcherActivity
                ?? activities.FirstOrDefault();

            return (activities, selectedActivity);
        }

        #endregion

        #region Private Methods - Activity Validation

        private bool IsLauncherActivity(PackageManager pm, string packageName, string activityName)
        {
            try
            {
                var intent = new Intent(Intent.ActionMain);
                intent.AddCategory(Intent.CategoryLauncher);
                intent.SetPackage(packageName);

                var activities = pm.QueryIntentActivities(intent, 0);
                return activities.Any(r => r.ActivityInfo.Name == activityName);
            }
            catch
            {
                return false;
            }
        }

        private bool SupportsViewAction(PackageManager pm, string packageName, string activityName)
        {
            try
            {
                var fileIntent = new Intent(Intent.ActionView);
                fileIntent.SetComponent(new global::Android.Content.ComponentName(packageName, activityName));
                fileIntent.SetData(global::Android.Net.Uri.Parse("file:///test.rom"));

                var resolveInfo1 = pm.ResolveActivity(fileIntent, 0);
                if (resolveInfo1?.ActivityInfo != null && resolveInfo1.ActivityInfo.Name == activityName)
                    return true;

                var contentIntent = new Intent(Intent.ActionView);
                contentIntent.SetComponent(new global::Android.Content.ComponentName(packageName, activityName));
                contentIntent.SetData(global::Android.Net.Uri.Parse("content://test"));

                var resolveInfo2 = pm.ResolveActivity(contentIntent, 0);
                if (resolveInfo2?.ActivityInfo != null && resolveInfo2.ActivityInfo.Name == activityName)
                    return true;

                var packageInfo = pm.GetPackageInfo(packageName, PackageInfoFlags.Activities);
                if (packageInfo?.Activities != null)
                {
                    var activity = packageInfo.Activities.FirstOrDefault(a => a.Name == activityName);
                    if (activity != null)
                    {
                        if (activity.Exported && !IsLauncherActivity(pm, packageName, activityName))
                            return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private Methods - Conversion

        private Avalonia.Media.Imaging.Bitmap? ConvertDrawableToAvaloniaBitmap(Drawable drawable, global::Android.Content.Context context)
        {
            try
            {
                int width = drawable.IntrinsicWidth;
                int height = drawable.IntrinsicHeight;

                if (width <= 0 || height <= 0)
                {
                    var metrics = context.Resources.DisplayMetrics;
                    width = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 48, metrics);
                    height = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 48, metrics);
                }

                Bitmap androidBitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
                Canvas canvas = new Canvas(androidBitmap);
                drawable.SetBounds(0, 0, width, height);
                drawable.Draw(canvas);

                using (var ms = new MemoryStream())
                {
                    androidBitmap.Compress(Bitmap.CompressFormat.Png, 100, ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    androidBitmap.Recycle();
                    return new Avalonia.Media.Imaging.Bitmap(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}