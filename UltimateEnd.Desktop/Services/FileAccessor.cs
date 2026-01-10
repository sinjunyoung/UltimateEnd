using System;
using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class FileAccessor : IFileAccessor
    {
        public Stream? OpenRead(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;

                return File.OpenRead(path);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public bool Exists(string path) => File.Exists(path);
    }
}