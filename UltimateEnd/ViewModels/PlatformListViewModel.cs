using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Orchestrators;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.ViewModels
{
    public class PlatformListViewModel : ViewModelBase
    {

        #region Fields

        private int _selectedIndex = -1;
        private Platform? _selectedPlatform;
        private string _currentTime;
        private string _currentDate;
        private string _currentThemeName;
        private readonly Timer? _timer;
        private bool _isMenuFocused = false;        
        private bool _isCurrentlyLoading = false;
        private bool _triggerScrollFix;
        private readonly Random _random = new();
        private string _versionText;
        private int _platformCount;
        private bool _isInitialLoadComplete = false;

        #endregion

        #region Properties

        public RangeObservableCollection<Platform> Platforms { get; } = [];

        public ObservableCollection<ThemeOption> AvailableThemes { get; } = [];

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    this.RaiseAndSetIfChanged(ref _selectedIndex, value);
                    SelectedPlatform = Platforms.ElementAtOrDefault(value);
                }
            }
        }

        public Platform SelectedPlatform
        {
            get => _selectedPlatform;
            set => this.RaiseAndSetIfChanged(ref _selectedPlatform, value);
        }

        public string CurrentTime
        {
            get => _currentTime;
            set => this.RaiseAndSetIfChanged(ref _currentTime, value);
        }

        public string CurrentDate
        {
            get => _currentDate;
            set => this.RaiseAndSetIfChanged(ref _currentDate, value);
        }

        public bool IsMenuFocused
        {
            get => _isMenuFocused;
            set => this.RaiseAndSetIfChanged(ref _isMenuFocused, value);
        }

        public string CurrentThemeName
        {
            get => _currentThemeName;
            set => this.RaiseAndSetIfChanged(ref _currentThemeName, value);
        }

        public bool TriggerScrollFix
        {
            get => _triggerScrollFix;
            set => this.RaiseAndSetIfChanged(ref _triggerScrollFix, value);
        }

        public string VersionText
        {
            get => _versionText;
            set => this.RaiseAndSetIfChanged(ref _versionText, value);
        }

        public int PlatformCount
        {
            get => _platformCount;
            set => this.RaiseAndSetIfChanged(ref _platformCount, value);
        }

        public bool IsInitialLoadComplete
        {
            get => _isInitialLoadComplete;
            private set => this.RaiseAndSetIfChanged(ref _isInitialLoadComplete, value);
        }

        #endregion

        #region Events

        public event Action<Platform>? PlatformSelected;
        public event EventHandler? EmulatorSettingViewRequested;
        public event EventHandler<int>? ScreensaverTimeoutChanged;
        public event EventHandler? KeyBindingSettingViewRequested;

        #endregion

        #region Constructor

        public PlatformListViewModel()
        {
            _currentTime = string.Empty;
            _currentDate = string.Empty;
            _currentThemeName = string.Empty;
            _selectedPlatform = null;

            var ver = PlatformServiceFactory.Create?.Invoke();
            VersionText = ver != null ? $"{ver.GetAppName() ?? "Unknown"} Ver {ver.GetAppVersion() ?? "0.0"}" : "Unknown Version";

            SettingsService.PlatformSettingsChanged += OnPlatformSettingsChanged;
            ThemeService.ThemeChanged += OnThemeChanged;

            LoadThemesFromService();
            UpdateDateTime();

            _timer = new Timer(_ => UpdateDateTime(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

            _ = LoadPlatformsAsync();
        }

        #endregion

        #region Event Handlers

        private void OnPlatformSettingsChanged()
        {
            PlatformMappingService.Instance.ClearCache();

            _ = LoadPlatformsAsync();

            _ = Task.Run(() =>
            {
                AllGamesManager.Instance.Clear();
                var settings = SettingsService.LoadSettings();
                var platformKeys = settings.PlatformSettings?.Keys.ToList();

                if (platformKeys?.Count > 0)
                {
                    MetadataService.PreloadHasGamesCache(platformKeys);
                    AllGamesManager.Instance.StartFullLoad();
                }
            });
        }

        private void OnThemeChanged(string theme) => LoadThemesFromService();

        public void OnShowEmulatorSettingViewRequested() => EmulatorSettingViewRequested?.Invoke(this, EventArgs.Empty);

        public void OnScreensaverTimeoutChangedRequested(int minute) => ScreensaverTimeoutChanged?.Invoke(this, minute);

        public void OnShowKeyBindingSettingViewRequested() => KeyBindingSettingViewRequested?.Invoke(this, EventArgs.Empty);

        #endregion

        #region Theme Management

        private void LoadThemesFromService()
        {
            AvailableThemes.Clear();

            foreach (var theme in ThemeService.GetAvailableThemes()) AvailableThemes.Add(theme);

            CurrentThemeName = ThemeService.GetCurrentThemeName();
        }

        public static void SelectTheme(ThemeOption theme) => ThemeService.ApplyTheme(theme.Name);

        #endregion

        #region Platform Loading

        public async Task LoadPlatformsAsync()
        {
            if (_isCurrentlyLoading) return;

            _isCurrentlyLoading = true;

            try
            {
                var savedSettings = SettingsService.LoadSettings();
                var mappingConfig = PlatformMappingService.Instance.LoadMapping();
                var currentSelectedId = SelectedPlatform?.Id;

                List<Platform> allPlatforms = [.. GetSpecialPlatforms()];

                if (savedSettings.PlatformSettings != null)
                {
                    var platformList = savedSettings.PlatformSettings.ToList();
                    var regularPlatforms = await LoadPlatformsInParallelAsync(platformList, savedSettings, mappingConfig);
                    allPlatforms.AddRange(regularPlatforms);
                }

                if (savedSettings.PlatformOrder != null && savedSettings.PlatformOrder.Count > 0)
                {
                    allPlatforms = [.. allPlatforms
                        .OrderBy(p =>
                        {
                            var index = savedSettings.PlatformOrder.IndexOf(p.Id);

                            return index == -1 ? int.MaxValue : index;
                        })];
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Platforms.Clear();
                    Platforms.AddRange(allPlatforms);
                    RestoreSelection(currentSelectedId);
                    PlatformCount = Platforms.Count;
                }, DispatcherPriority.Normal);

                IsInitialLoadComplete = true;
            }
            finally
            {
                _isCurrentlyLoading = false;

                await Dispatcher.UIThread.InvokeAsync(() => TriggerScrollFix = !TriggerScrollFix, DispatcherPriority.Background);
            }
        }

        private void RestoreSelection(string currentSelectedId)
        {
            if (Platforms.Count == 0) return;

            if (!string.IsNullOrEmpty(currentSelectedId))
            {
                var index = Platforms.ToList().FindIndex(p => p.Id == currentSelectedId);
                SelectedIndex = index >= 0 ? index : 0;
            }
            else
                SelectedIndex = 0;
        }

        private static async Task<List<Platform>> LoadPlatformsInParallelAsync(List<KeyValuePair<string, PlatformSettings>> platformList, AppSettings settings, PlatformMappingConfig mappingConfig)
        {
            return await Task.Run(() =>
            {
                var tasks = platformList.Select(platformSetting =>
                {
                    try
                    {
                        if (platformSetting.Key == GameMetadataManager.SteamKey || platformSetting.Key == GameMetadataManager.DesktopKey || platformSetting.Key == GameMetadataManager.AndroidKey) return (Platform?)null;

                        if (!mappingConfig.FolderMappings.ContainsKey(platformSetting.Key)) return (Platform?)null;

                        bool hasGames = MetadataService.HasGames(platformSetting.Key);

                        if (!hasGames) return (Platform?)null;

                        var displayName = GetPlatformDisplayName(platformSetting.Key, mappingConfig);
                        var normalizedId = GetNormalizedPlatformId(platformSetting.Key);

                        var imagePath = GetPlatformImagePath(platformSetting, settings.RomsBasePaths[0], normalizedId);
                        var logoPath = ResourceHelper.GetLogoImage(normalizedId);

                        return new Platform
                        {
                            Id = platformSetting.Key,
                            Name = displayName,
                            ImagePath = imagePath,
                            LogoPath = logoPath,
                            FolderPath = platformSetting.Key,
                            MappedPlatformId = normalizedId
                        };
                    }
                    catch
                    {
                        return (Platform?)null;
                    }
                }).ToArray();

                var platforms = new Platform?[tasks.Length];

                Parallel.For(0, tasks.Length, i => { platforms[i] = tasks[i]; });

                var groupedPlatforms = platforms
                    .Where(p => p != null)
                    .GroupBy(p => p!.MappedPlatformId)
                    .Select(group =>
                    {
                        var mappedId = group.Key;
                        var representative = group.OrderBy(p => p!.Id).First()!;
                        return new Platform
                        {
                            Id = mappedId,
                            Name = PlatformInfoService.Instance.GetPlatformDisplayName(mappedId),
                            ImagePath = representative.ImagePath,
                            LogoPath = representative.LogoPath,
                            FolderPath = representative.FolderPath,
                            MappedPlatformId = mappedId,
                        };
                    })
                    .ToList();

                return groupedPlatforms;
            });
        }

        private static List<Platform> GetSpecialPlatforms()
        {
            List<Platform> platforms = [ 
                CreatePlatform(GameMetadataManager.AllGamesKey, "전체", GameMetadataManager.AllGamesKey),
                CreatePlatform(GameMetadataManager.FavoritesKey, "즐겨찾기", GameMetadataManager.FavoritesKey),
                CreatePlatform(GameMetadataManager.HistoriesKey, "플레이 기록", GameMetadataManager.HistoriesKey) ];

            var playlists = PlaylistManager.Instance.GetAllPlaylists();

            foreach (var playlist in playlists) platforms.Add(playlist.ToPlatform());

            if (OperatingSystem.IsWindows())
            {
                var steamGames = AllGamesManager.Instance.GetPlatformGames(GameMetadataManager.SteamKey);

                if (steamGames.Count > 0) platforms.Add(CreatePlatform(GameMetadataManager.SteamKey, "Steam", GameMetadataManager.SteamKey));
            }

            var settings = SettingsService.LoadSettings();

            if (settings.ShowNativeAppPlatform)
            {
                var appProvider = AppProviderFactory.Create?.Invoke();

                if (appProvider != null) platforms.Add(CreatePlatform(appProvider.PlatformId, appProvider.PlatformName, appProvider.PlatformId));
            }

            return platforms;
        }

        private static Platform CreatePlatform(string id, string name, string platform)
        {
            return new()
            {
                Id = id,
                Name = name,
                ImagePath = ResourceHelper.GetPlatformImage(platform),
                LogoPath = ResourceHelper.GetLogoImage(platform)
            };
        }

        #endregion

        public async Task LaunchRandomGame()
        {
            var allGames = AllGamesManager.Instance.GetAllGames()
                .Where(g => !g.Ignore)
                .ToList();

            if (allGames.Count == 0)
            {
                await DialogService.Instance.ShowMessage("알림", "실행 가능한 게임이 없습니다.", MessageType.Warning);
                return;
            }

            var favorites = allGames.Where(g => g.IsFavorite).ToList();
            var nonFavorites = allGames.Where(g => !g.IsFavorite).ToList();

            GameMetadata selectedGame;

            if (favorites.Count > 0 && nonFavorites.Count > 0)
            {
                if (_random.NextDouble() < 0.2)
                    selectedGame = favorites[_random.Next(favorites.Count)];
                else
                    selectedGame = nonFavorites[_random.Next(nonFavorites.Count)];
            }
            else if (favorites.Count > 0)
                selectedGame = favorites[_random.Next(favorites.Count)];
            else if (nonFavorites.Count > 0)
                selectedGame = nonFavorites[_random.Next(nonFavorites.Count)];
            else
            {
                await DialogService.Instance.ShowMessage("알림", "실행 가능한 게임이 없습니다.", MessageType.Warning);
                return;
            }

            var orchestrator = new GameLaunchOrchestrator(null);
            await orchestrator.LaunchAsync(selectedGame);
        }

        #region Platform Helpers

        private static string GetPlatformDisplayName(string platformKey, PlatformMappingConfig mappingConfig)
        {
            if (mappingConfig?.CustomDisplayNames?.TryGetValue(platformKey, out var customName) == true && !string.IsNullOrEmpty(customName)) return customName;

            var converter = PathConverterFactory.Create?.Invoke();
            var realPath = converter?.FriendlyPathToRealPath(platformKey) ?? platformKey;
            var mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(realPath);
            var platformDisplayName = !string.IsNullOrEmpty(mappedPlatformId) ? PlatformInfoService.Instance.GetPlatformDisplayName(mappedPlatformId) : PlatformInfoService.Instance.GetPlatformDisplayName(platformKey);
            var normalizedFolder = PlatformInfoService.Instance.NormalizePlatformId(platformKey);
            var normalizedDisplay = PlatformInfoService.Instance.NormalizePlatformId(platformDisplayName);

            return normalizedFolder == normalizedDisplay ? platformDisplayName : $"{platformDisplayName} ({platformKey})";
        }

        private static string GetNormalizedPlatformId(string platformKey)
        {
            var converter = PathConverterFactory.Create?.Invoke();
            var realPath = converter?.FriendlyPathToRealPath(platformKey) ?? platformKey;

            return PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? PlatformInfoService.Instance.NormalizePlatformId(platformKey);
        }

        private static string GetPlatformImagePath(KeyValuePair<string, PlatformSettings> platformSetting, string romsBasePath, string normalizedId)
        {
            var settings = SettingsService.LoadSettings();

            if (settings.PlatformImages.TryGetValue(normalizedId, out var savedImage) && !string.IsNullOrEmpty(savedImage)) return savedImage;

            var imagePath = string.IsNullOrEmpty(platformSetting.Value.ImagePath) ? Path.Combine(romsBasePath, platformSetting.Key, "platform.png") : platformSetting.Value.ImagePath;
            var converter = PathConverterFactory.Create?.Invoke();
            var realImagePath = converter?.FriendlyPathToRealPath(imagePath) ?? imagePath;

            return File.Exists(realImagePath) ? imagePath : ResourceHelper.GetPlatformImage(normalizedId);
        }

        #endregion

        #region Navigation
        public void UpdateFavorites()
        {
            int currentCount = FavoritesManager.Count;
            var favoritePlatform = Platforms.FirstOrDefault(p => p.Id == GameMetadataManager.FavoritesKey);

            if (currentCount > 0 && favoritePlatform == null)
                Platforms.Insert(1, CreatePlatform(GameMetadataManager.FavoritesKey, "즐겨찾기", GameMetadataManager.FavoritesKey));
            else if (currentCount == 0 && favoritePlatform != null)
            {
                Platforms.Remove(favoritePlatform);

                if (SelectedPlatform?.Id == GameMetadataManager.FavoritesKey) SelectedIndex = 0;
            }
        }

        public void MoveLeft()
        {
            if (Platforms.Count == 0) return;

            if (SelectedIndex == 0) SelectedIndex = Platforms.Count - 1;
            else SelectedIndex--;
        }

        public void MoveRight()
        {
            if (Platforms.Count == 0) return;

            if (SelectedIndex == Platforms.Count - 1) SelectedIndex = 0;
            else SelectedIndex++;
        }

        public void SelectCurrentPlatform()
        {
            if (SelectedPlatform != null) PlatformSelected?.Invoke(SelectedPlatform);
        }
        #endregion

        #region Utilities

        private void UpdateDateTime()
        {
            var now = DateTime.Now;

            CurrentTime = now.ToString("HH:mm");
            CurrentDate = now.ToString("yyyy-MM-dd");
        }

        public void Dispose()
        {
            _timer?.Dispose();

            SettingsService.PlatformSettingsChanged -= OnPlatformSettingsChanged;
            ThemeService.ThemeChanged -= OnThemeChanged;
        }

        public void SavePlatformOrder()
        {
            var settings = SettingsService.LoadSettings();
            settings.PlatformOrder = [.. Platforms.Select(p => p.Id)];

            SettingsService.SaveSettingsQuiet(settings);
        }

        #endregion
    }
}