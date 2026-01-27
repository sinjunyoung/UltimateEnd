using Android.App;
using Android.Content;
using Android.OS;

namespace UltimateEnd.Android.Utils
{
    public static class ApkInstaller
    {
        public static void Install(Activity activity, string apkPath)
        {
            var file = new Java.IO.File(apkPath);
            global::Android.Net.Uri apkUri;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                var fileProviderClass = Java.Lang.Class.ForName("androidx.core.content.FileProvider");
                var method = fileProviderClass.GetMethod("getUriForFile", Java.Lang.Class.FromType(typeof(Context)), Java.Lang.Class.FromType(typeof(Java.Lang.String)), Java.Lang.Class.FromType(typeof(Java.IO.File)));
                apkUri = (global::Android.Net.Uri)method.Invoke(null, activity, $"{activity.PackageName}.fileprovider", file);
            }
            else
                apkUri = global::Android.Net.Uri.FromFile(file);

            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(apkUri, "application/vnd.android.package-archive");
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
            activity.StartActivity(intent);
        }
    }
}