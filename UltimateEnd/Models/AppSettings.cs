using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using UltimateEnd.Enums;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Models
{
    public class AppSettings
    {
        #region Constants

        private const double MIN_SPLITTER_RATIO = 0.3;
        private const double MIN_VERTICAL_SPLITTER_RATIO = 0.5;
        private const double DefaultGameListSplitterPosition = 1;
        private const double DefaultGameListVerticalSplitterPosition = 1.5;

        #endregion

        #region Fields

        private List<string> _romsBasePaths = [];
        private Dictionary<string, PlatformSettings> _platformSettings = [];
        private double _gameListSplitterPosition = DefaultGameListSplitterPosition;
        private double _gameListVerticalSplitterPosition = DefaultGameListVerticalSplitterPosition;
        private string? _backgroundImagePath;
        private Dictionary<string, string> _platformImages = [];

        #endregion

        public int ActiveGamepadIndex { get; set; } = 0;

        public int GridColumns { get; set; } = 7;

        public bool SimpleGameListMode { get; set; } = false;

        #region ROM Base Paths

        [JsonIgnore]
        public List<string> RomsBasePaths
        {
            get => _romsBasePaths;
            set
            {
                _romsBasePaths = value ?? [];

                if (_romsBasePaths.Count > 0)
                {
                    try
                    {
                        PathHelper.Initialize(_romsBasePaths);
                    }
                    catch { }
                }
            }
        }

        [JsonPropertyName("romsBasePaths")]
        public List<string> RomsBasePathsData
        {
            get => _romsBasePaths;
            set => RomsBasePaths = value;
        }

        #endregion

        #region Platform Settings

        [JsonIgnore]
        public Dictionary<string, PlatformSettings> PlatformSettings
        {
            get => _platformSettings;
            set => _platformSettings = value;
        }

        [JsonPropertyName("PlatformSettings")]
        public Dictionary<string, PlatformSettings> PlatformSettingsForSerialization
        {
            get
            {
                var result = new Dictionary<string, PlatformSettings>();
                var converter = PathConverterFactory.Create?.Invoke();

                foreach (var kvp in _platformSettings)
                {
                    var friendlyKey = converter?.RealPathToFriendlyPath(kvp.Key) ?? kvp.Key;

                    result[friendlyKey] = new PlatformSettings
                    {
                        Name = kvp.Value.Name,
                        BasePath = PathHelper.ToRelativePath(kvp.Value.BasePath),
                        ImagePath = string.IsNullOrEmpty(kvp.Value.ImagePath) ? kvp.Value.ImagePath : PathHelper.ToRelativePath(kvp.Value.ImagePath)
                    };
                }

                return result;
            }
            set
            {
                _platformSettings = [];
                var converter = PathConverterFactory.Create?.Invoke();

                if (value != null)
                {
                    foreach (var kvp in value)
                    {
                        var realKey = converter?.FriendlyPathToRealPath(kvp.Key) ?? kvp.Key;

                        _platformSettings[realKey] = new PlatformSettings
                        {
                            Name = kvp.Value.Name,
                            BasePath = string.IsNullOrEmpty(kvp.Value.BasePath) ? string.Empty : PathHelper.ToAbsolutePath(kvp.Value.BasePath),
                            ImagePath = string.IsNullOrEmpty(kvp.Value.ImagePath) ? kvp.Value.ImagePath : PathHelper.ToAbsolutePath(kvp.Value.ImagePath)
                        };
                    }
                }
            }
        }

        public List<string>? PlatformOrder { get; set; }

        public bool ShowNativeAppPlatform { get; set; } = false;

        #endregion

        #region Platform Images

        [JsonIgnore]
        public Dictionary<string, string> PlatformImages
        {
            get => _platformImages;
            set => _platformImages = value;
        }

        [JsonPropertyName("platformImages")]
        public Dictionary<string, string> PlatformImagesForSerialization
        {
            get
            {
                Dictionary<string, string> result = [];

                foreach (var kvp in _platformImages) result[kvp.Key] = string.IsNullOrEmpty(kvp.Value) ? kvp.Value : PathHelper.ToRelativePath(kvp.Value);

                return result;
            }
            set
            {
                _platformImages = [];

                if (value != null)
                    foreach (var kvp in value) _platformImages[kvp.Key] = string.IsNullOrEmpty(kvp.Value) ? kvp.Value : PathHelper.ToAbsolutePath(kvp.Value);
            }
        }

        #endregion

        #region UI Settings

        public string Theme { get; set; } = "DarkTheme";

        public double GameListSplitterPosition
        {
            get => _gameListSplitterPosition;
            set => _gameListSplitterPosition = Math.Max(value, MIN_SPLITTER_RATIO);
        }

        public double GameListVerticalSplitterPosition
        {
            get => _gameListVerticalSplitterPosition;
            set => _gameListVerticalSplitterPosition = Math.Max(value, MIN_VERTICAL_SPLITTER_RATIO);
        }

        [JsonPropertyName("gameViewMode")]
        public GameViewMode GameViewMode { get; set; }

        [JsonIgnore]
        public string? BackgroundImagePath
        {
            get => _backgroundImagePath;
            set => _backgroundImagePath = value;
        }

        [JsonPropertyName("backgroundImagePath")]
        public string? BackgroundImagePathForSerialization
        {
            get => string.IsNullOrEmpty(_backgroundImagePath) ? _backgroundImagePath : PathHelper.ToRelativePath(_backgroundImagePath);
            set => _backgroundImagePath = string.IsNullOrEmpty(value) ? value : PathHelper.ToAbsolutePath(value);
        }

        #endregion

        #region Input Settings

        public Dictionary<string, string> KeyBindings { get; set; } = [];

        public Dictionary<string, int> GamepadButtonMapping { get; set; } = [];

        public string? LastDetectedControllerType { get; set; }

        #endregion

        #region Screensaver Settings

        [JsonPropertyName("screensaverTimeoutMinutes")]
        public int ScreensaverTimeoutMinutes { get; set; } = 5;

        [JsonPropertyName("screensaverVideoChangeInterval")]
        public int ScreensaverVideoChangeInterval { get; set; } = 30;

        [JsonIgnore]
        public bool ScreensaverEnabled => ScreensaverTimeoutMinutes > 0;

        [JsonIgnore]
        public TimeSpan ScreensaverTimeout => TimeSpan.FromMinutes(ScreensaverTimeoutMinutes);

        [JsonIgnore]
        public TimeSpan VideoChangeInterval => TimeSpan.FromSeconds(ScreensaverVideoChangeInterval);

        #endregion

        #region Save Settings

        public SaveBackupMode SaveBackupMode { get; set; } = SaveBackupMode.NormalSave;

        #endregion

        #region Static Properties

        [JsonIgnore]
        public static string SystemAppsPath
        {
            get
            {
                var provider = AppBaseFolderProviderFactory.Create?.Invoke();

                return provider?.GetSystemAppsFolder() ?? string.Empty;
            }
        }

        #endregion

        #region Methods

        public void ResetGameListViewLayout()
        {
            GameListSplitterPosition = DefaultGameListSplitterPosition;
            GameListVerticalSplitterPosition = DefaultGameListVerticalSplitterPosition;
        }

        #endregion
    }
}