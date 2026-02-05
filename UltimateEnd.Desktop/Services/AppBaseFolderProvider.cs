using System;
using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class AppBaseFolderProvider : IAppBaseFolderProvider
    {
        public string GetAssetsFolder()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets");

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public string GetSettingsFolder()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "settings");

            if(!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public string GetPlatformsFolder()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "platforms");

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public string GetSystemAppsFolder()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "systemapps");

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }
    }
}