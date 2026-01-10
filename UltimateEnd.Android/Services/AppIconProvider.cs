using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using System;
using System.IO;
using UltimateEnd.Android.Utils;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class AppIconProvider : IAppIconProvider
    {
        public Avalonia.Media.Imaging.Bitmap GetAppIcon(string command)
        {
            string packageName = CommandLineParser.ExtractPackageName(command);

            if (string.IsNullOrEmpty(packageName)) return null;

            var context = AndroidApplication.AppContext;
            var pm = context.PackageManager;

            try
            {
                var drawable = pm.GetApplicationIcon(packageName);

                if (drawable is Drawable bd)
                {
                    int width = drawable.IntrinsicWidth;
                    int height = drawable.IntrinsicHeight;

                    if (width <= 0 || height <= 0)
                    {
                        var metrics = context.Resources.DisplayMetrics;
                        width = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 32, metrics);
                        height = (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, 32, metrics);
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

                return null;
            }
            catch (Exception ex)
            {
                Log.Error("AppIconProvider", $"아이콘 가져오기 실패 ({packageName}): {ex.Message}");
                return null;
            }
        }
    }
}