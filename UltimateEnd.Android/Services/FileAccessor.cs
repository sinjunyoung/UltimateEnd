using Android.Content;
using System;
using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class FileAccessor(Context context) : IFileAccessor
    {
        private readonly Context _context = context;

        public Stream? OpenRead(string path)
        {
            try
            {
                if (path.StartsWith("content://"))
                {
                    var uri = global::Android.Net.Uri.Parse(path);
                    var stream = _context.ContentResolver?.OpenInputStream(uri);

                    if (stream == null)
                        return null;

                    return stream;
                }
                else
                {
                    if (!File.Exists(path))
                        return null;

                    return File.OpenRead(path);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool Exists(string path)
        {
            try
            {
                if (path.StartsWith("content://"))
                {
                    var decodedPath = global::Android.Net.Uri.Decode(path);
                    var uri = global::Android.Net.Uri.Parse(decodedPath);

                    var stream = _context.ContentResolver?.OpenInputStream(uri);

                    if (stream != null)
                    {
                        stream.Dispose();
                        return true;
                    }

                    return false;
                }
                else
                {
                    return File.Exists(path);
                }
            }
            catch
            {
                return false;
            }
        }
    }
}