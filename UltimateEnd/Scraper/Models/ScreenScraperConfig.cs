using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using UltimateEnd.Enums;
using UltimateEnd.Services;

namespace UltimateEnd.Scraper.Models
{
    public class ScreenScraperConfig
    {
        private static readonly Lazy<ScreenScraperConfig> _instance = new(() => Load());
        private static readonly Lock _lockObject = new();

        [JsonIgnore]
        public string ApiDevU { get; } = Environment.GetEnvironmentVariable("SCREENSCRAPER_DEV_U") ?? "cdh5";

        [JsonIgnore]
        public string ApiDevP { get; } = Environment.GetEnvironmentVariable("SCREENSCRAPER_DEV_P") ?? "4uYSPOGJlWP";

        [JsonIgnore]
        public string ApiSoftName { get; } = "UltimateEnd";

        [JsonIgnore]
        public string ApiUrlBase { get; } = "https://api.screenscraper.fr/api2";

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    Save();
                }
            }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    Save();
                }
            }
        }

        private int _maxConcurrentConnections  = 3;
        public int MaxConcurrentConnections
        {
            get => _maxConcurrentConnections;
            set
            {
                if (_maxConcurrentConnections != value)
                {
                    _maxConcurrentConnections = value;
                    Save();
                }
            }
        }

        private int _delayBetweenRequestsMs = 500;
        public int DelayBetweenRequestsMs
        {
            get => _delayBetweenRequestsMs;
            set
            {
                var newValue = value < 0 ? 0 : value;
                if (_delayBetweenRequestsMs != newValue)
                {
                    _delayBetweenRequestsMs = newValue;
                    Save();
                }
            }
        }

        private int _httpTimeoutSeconds = 120;
        public int HttpTimeoutSeconds
        {
            get => _httpTimeoutSeconds;
            set
            {
                if (value > 0 && _httpTimeoutSeconds != value)
                {
                    _httpTimeoutSeconds = value;
                    Save();
                }
            }
        }

        private int _connectionTimeoutSeconds = 5;
        public int ConnectionTimeoutSeconds
        {
            get => _connectionTimeoutSeconds;
            set
            {
                if (value > 0 && _connectionTimeoutSeconds != value)
                {
                    _connectionTimeoutSeconds = value;
                    Save();
                }
            }
        }

        private string _preferredLanguage = "jp";
        public string PreferredLanguage
        {
            get => _preferredLanguage;
            set
            {
                if (_preferredLanguage != value)
                {
                    _preferredLanguage = value;
                    Save();
                    PreferredLanguageChanged?.Invoke();
                }
            }
        }

        private string _regionStyle = "Japanese";
        public string RegionStyle
        {
            get => _regionStyle;
            set
            {
                if (_regionStyle != value)
                {
                    _regionStyle = value;
                    Save();
                    PreferredRegionsChanged?.Invoke();
                }
            }
        }

        private LogoImageType _logoImage = LogoImageType.Normal;
        public LogoImageType LogoImage
        {
            get => _logoImage;
            set
            {
                if (_logoImage != value)
                {
                    _logoImage = value;
                    Save();
                }
            }
        }

        private CoverImageType _coverImage = CoverImageType.Box2D;
        public CoverImageType CoverImage
        {
            get => _coverImage;
            set
            {
                if (_coverImage != value)
                {
                    _coverImage = value;
                    Save();
                }
            }
        }

        private bool _useZipInternalFileName = false;
        public bool UseZipInternalFileName
        {
            get => _useZipInternalFileName;
            set
            {
                if (_useZipInternalFileName != value)
                {
                    _useZipInternalFileName = value;
                    Save();
                }
            }
        }

        private bool _allowScrapTitle = false;
        public bool AllowScrapTitle
        {
            get => _allowScrapTitle;
            set
            {
                if (_allowScrapTitle != value)
                {
                    _allowScrapTitle = value;
                    Save();
                }
            }
        }

        private bool _allowScrapDescription = true;
        public bool AllowScrapDescription
        {
            get => _allowScrapDescription;
            set
            {
                if (_allowScrapDescription != value)
                {
                    _allowScrapDescription = value;
                    Save();
                }
            }
        }

        private bool _allowScrapLogo = true;
        public bool AllowScrapLogo
        {
            get => _allowScrapLogo;
            set
            {
                if (_allowScrapLogo != value)
                {
                    _allowScrapLogo = value;
                    Save();
                }
            }
        }

        private bool _allowScrapCover = true;
        public bool AllowScrapCover
        {
            get => _allowScrapCover;
            set
            {
                if (_allowScrapCover != value)
                {
                    _allowScrapCover = value;
                    Save();
                }
            }
        }

        private bool _allowScrapVideo = true;
        public bool AllowScrapVideo
        {
            get => _allowScrapVideo;
            set
            {
                if (_allowScrapVideo != value)
                {
                    _allowScrapVideo = value;
                    Save();
                }
            }
        }

        private ScrapCondition _scrapConditionType = ScrapCondition.AllMediaMissing;
        public ScrapCondition ScrapConditionType
        {
            get => _scrapConditionType;
            set
            {
                if (_scrapConditionType != value)
                {
                    _scrapConditionType = value;
                    Save();
                }
            }
        }

        [JsonIgnore]
        public TimeSpan HttpTimeout => TimeSpan.FromSeconds(HttpTimeoutSeconds);

        [JsonIgnore]
        public TimeSpan ConnectionTimeout => TimeSpan.FromSeconds(ConnectionTimeoutSeconds);

        private SearchMethod _preferredSearchMethod = SearchMethod.ByFileName;
        public SearchMethod PreferredSearchMethod
        {
            get => _preferredSearchMethod;
            set
            {
                if (_preferredSearchMethod != value)
                {
                    _preferredSearchMethod = value;
                    Save();
                }
            }
        }

        private bool _enableAutoScrap = false;
        public bool EnableAutoScrap
        {
            get => _enableAutoScrap;
            set
            {
                if (_enableAutoScrap != value)
                {
                    _enableAutoScrap = value;
                    Save();
                }
            }
        }   

        [JsonIgnore]
        public static int CrcCalculationMaxSizeMB 
        { 
            get
            {
                if (OperatingSystem.IsAndroid())
                    return 256;

                return 99999999;
            }
        }

        private const string SettingsFileName = "scrap_settings.json";

        public static event Action? PreferredLanguageChanged;
        public static event Action? PreferredRegionsChanged;

        public static ScreenScraperConfig Instance => _instance.Value;

        public ScreenScraperConfig()
        {
        }

        public void Save()
        {
            lock (_lockObject)
            {
                try
                {
                    var filePath = GetSettingsFilePath();
                    var directory = Path.GetDirectoryName(filePath);

                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    var json = JsonSerializer.Serialize(this, options);
                    File.WriteAllText(filePath, json);
                }
                catch
                {
                    throw;
                }
            }
        }

        private static ScreenScraperConfig Load()
        {
            lock (_lockObject)
            {
                try
                {
                    var filePath = GetSettingsFilePath();

                    if (!File.Exists(filePath))
                        return new ScreenScraperConfig();

                    var json = File.ReadAllText(filePath);
                    var config = JsonSerializer.Deserialize<ScreenScraperConfig>(json);

                    return config ?? new ScreenScraperConfig();
                }
                catch
                {
                    return new ScreenScraperConfig();
                }
            }
        }

        public string[] GetPreferredRegions()
        {
            if (RegionStyle == "Japanese")
                return ["kr", "jp", "ss", "wor", "eu", "us"];
            else
                return ["kr", "wor", "eu", "us", "ss", "jp"];
        }

        public string GetLanguageCode()
        {
            return PreferredLanguage;
        }

        public void Reset()
        {
            _username = string.Empty;
            _password = string.Empty;
            _maxConcurrentConnections = 3;
            _delayBetweenRequestsMs = 500;
            _httpTimeoutSeconds = 120;
            _connectionTimeoutSeconds = 5;
            _preferredLanguage = "jp";
            _regionStyle = "Japanese";
            _logoImage = LogoImageType.Normal;
            _coverImage = CoverImageType.Box2D;
            _useZipInternalFileName = false;
            _allowScrapTitle = false;
            _allowScrapDescription = true;
            _allowScrapLogo = true;
            _allowScrapCover = true;
            _allowScrapVideo = true;
            _scrapConditionType = ScrapCondition.AllMediaMissing;
            PreferredSearchMethod = SearchMethod.ByFileName;
            EnableAutoScrap = false;
            Save();
        }

        private static string GetSettingsFilePath()
        {
            var provider = AppBaseFolderProviderFactory.Create?.Invoke();

            if (provider != null)
                return Path.Combine(provider.GetAppBaseFolder(), SettingsFileName);

            return Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        }
    }
}