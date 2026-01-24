using System.IO;
using UltimateEnd.Services;
using Android.Content;
using Application = global::Android.App.Application;

namespace UltimateEnd.Android.Services
{
    public class AppBaseFolderProvider : IAppBaseFolderProvider
    {
        private string _cachedFolder;
        private string _cachedPlatformsFolder;
        private string _cachedSystemAppsFolder;
        private static bool _firstRunChecked = false;

        public string GetSettingsFolder()
        {
            if (!string.IsNullOrEmpty(_cachedFolder)) return _cachedFolder;

            var settingsFolder = GetOrCreateSubFolder("settings");

            if (!string.IsNullOrEmpty(settingsFolder))
            {
                GetOrCreateSubFolder("Themes");
                CheckAndHandleFirstRun(settingsFolder);

                _cachedFolder = settingsFolder;

                return _cachedFolder;
            }

            var context = Application.Context;
            _cachedFolder = context.FilesDir?.AbsolutePath ?? string.Empty;

            return _cachedFolder;
        }

        public string GetPlatformsFolder()
        {
            if (!string.IsNullOrEmpty(_cachedPlatformsFolder)) return _cachedPlatformsFolder;

            _cachedPlatformsFolder = GetOrCreateSubFolder("platforms") ?? string.Empty;

            return _cachedPlatformsFolder;
        }

        public string GetSystemAppsFolder()
        {
            if (!string.IsNullOrEmpty(_cachedSystemAppsFolder)) return _cachedSystemAppsFolder;

            _cachedSystemAppsFolder = GetOrCreateSubFolder("systemapps") ?? string.Empty;

            return _cachedSystemAppsFolder;
        }

        private static string GetOrCreateSubFolder(string subFolderName)
        {
            var internalStorage = global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;

            if (string.IsNullOrEmpty(internalStorage)) return null;

            var baseFolder = Path.Combine(internalStorage, "UltimateEnd");

            if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

            var subFolder = Path.Combine(baseFolder, subFolderName);

            if (!Directory.Exists(subFolder)) Directory.CreateDirectory(subFolder);

            return subFolder;
        }

        private static void CheckAndHandleFirstRun(string settingsFolder)
        {
            if (_firstRunChecked) return;

            var context = Application.Context;
            var prefs = context.GetSharedPreferences("app_state", FileCreationMode.Private);
            bool isFirstRun = prefs.GetBoolean("is_first_run", true);

            if (isFirstRun)
            {
                string[] settingsFiles = ["settings.json", "scrap_settings.json", "commands.txt", "genres.json", "platform_info.json"];
                DeleteFilesInFolder(settingsFolder, settingsFiles);

                var themesFolder = GetOrCreateSubFolder("Themes");
                string[] themeFiles = ["BlueTheme.axaml", "CyberpunkTheme.axaml", "DarkTheme.axaml", "LightTheme.axaml"];
                DeleteFilesInFolder(themesFolder, themeFiles);

                var editor = prefs.Edit();
                editor.PutBoolean("is_first_run", false);
                editor.Apply();
            }

            _firstRunChecked = true;
        }

        private static void DeleteFilesInFolder(string folderPath, string[] fileNames)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            foreach (string fileName in fileNames)
            {
                var filePath = Path.Combine(folderPath, fileName);

                if (File.Exists(filePath)) File.Delete(filePath);
            }
        }
    }
}