using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class SettingsService
    {
        private const string SettingsFileName = "settings.json";
        private static AppSettings? _cachedSettings;

        public static event Action? PlatformSettingsChanged;
        public static event Action? GameListSettingsChanged;
        public static event Action? ThemeChanged;

        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly JsonSerializerOptions SerializeOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static string GetSettingsFilePath()
        {
            var provider = AppBaseFolderProviderFactory.Create?.Invoke();

            if (provider != null)
                return Path.Combine(provider.GetSettingsFolder(), SettingsFileName);

            return Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        }

        public static AppSettings LoadSettings()
        {
            if (_cachedSettings != null) return _cachedSettings;

            var filePath = GetSettingsFilePath();

            if (!File.Exists(filePath))
            {
                _cachedSettings = new AppSettings();

                return _cachedSettings;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, DeserializeOptions) ?? new AppSettings();
                _cachedSettings = settings;

                return _cachedSettings;
            }
            catch
            {
                _cachedSettings = new AppSettings();

                return _cachedSettings;
            }
        }

        private static void SaveSettings(AppSettings settings, Action? onSaveComplete = null)
        {
            _cachedSettings = null;

            try
            {
                var json = JsonSerializer.Serialize(settings, SerializeOptions);
                var filePath = GetSettingsFilePath();
                var directory = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(filePath, json);
                onSaveComplete?.Invoke();
            }
            catch { }
        }

        public static void SaveSettingsQuiet(AppSettings settings) => SaveSettings(settings);

        public static void SavePlatformSettings(AppSettings settings) => SaveSettings(settings, () => PlatformSettingsChanged?.Invoke());

        public static void SaveGameListSettings(AppSettings settings) => SaveSettings(settings, () => GameListSettingsChanged?.Invoke());

        public static void SaveThemeSettings(AppSettings settings) => SaveSettings(settings, () => ThemeChanged?.Invoke());

        public static string GetPlatformPath(string platformKey)
        {
            var settings = LoadSettings();
            var converter = PathConverterFactory.Create?.Invoke();

            string basePath = settings.PlatformSettings.TryGetValue(platformKey, out var platformSetting)
                ? platformSetting.BasePath
                : (settings.RomsBasePaths.FirstOrDefault() ?? string.Empty);

            var realBasePath = converter?.FriendlyPathToRealPath(basePath) ?? basePath;

            return Path.Combine(realBasePath, platformKey);
        }

        public static void InvokePlatformSettingsChanged() => PlatformSettingsChanged?.Invoke();
    }
}