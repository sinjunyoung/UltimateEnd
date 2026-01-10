using Avalonia;
using Avalonia.Media.Imaging;
using System;
using System.IO;
using UltimateEnd.Utils;

namespace UltimateEnd.Services
{
    public class GameImageLoader
    {
        public static Bitmap? LoadCoverImage(string path) => LoadAndResizeImage(path, ThumbnailSettings.GetMaxCoverWidth() * 1.3, useFileAccessor: false);

        public static Bitmap? LoadLogoImage(string path) => LoadAndResizeImage(path, ThumbnailSettings.GetMaxLogoWidth() * 2, useFileAccessor: true);

        private static Bitmap? LoadAndResizeImage(string path, double maxWidth, bool useFileAccessor)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                byte[] imageBytes = LoadImageBytes(path, useFileAccessor);

                if (imageBytes == null)
                    return null;

                using var ms = new MemoryStream(imageBytes);
                var originalBitmap = new Bitmap(ms);

                return ResizeIfNeeded(originalBitmap, (int)maxWidth);
            }
            catch
            {
                return null;
            }
        }

        private static byte[]? LoadImageBytes(string path, bool useFileAccessor)
        {
            if (useFileAccessor)
            {
                var fileAccessor = FileAccessorFactory.Create?.Invoke();

                if (fileAccessor?.Exists(path) != true)
                    return null;

                using var stream = fileAccessor.OpenRead(path);

                if (stream == null)
                    return null;

                using var ms = new MemoryStream();
                stream.CopyTo(ms);

                return ms.ToArray();
            }
            else
                return File.ReadAllBytes(path);
        }

        private static Bitmap ResizeIfNeeded(Bitmap originalBitmap, int maxWidth)
        {
            if (maxWidth >= 9999 || originalBitmap.PixelSize.Width <= maxWidth)
                return originalBitmap;

            var scale = (double)maxWidth / originalBitmap.PixelSize.Width;
            var newHeight = (int)(originalBitmap.PixelSize.Height * scale);

            var resized = originalBitmap.CreateScaledBitmap(new PixelSize(maxWidth, newHeight), BitmapInterpolationMode.HighQuality);

            originalBitmap.Dispose();

            return resized;
        }
    }
}