using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.ViewModels
{
    public class RomSettingViewModel : ViewModelBase, IDisposable
    {
        #region Constants

        private const int LOADING_OVERLAY_THRESHOLD = 50;

        #endregion

        #region Fields

        private readonly List<PlatformOption>? _cachedAvailablePlatforms;
        private readonly ICommandConfig? _cachedConfig;
        private bool _isLoading;
        private string _loadingMessage = "폴더 스캔중...";
        private int _loadingProgress = 0;
        private int _totalPlatforms;
        private bool _isCurrentlyLoading = false;
        private bool _useLoadingOverlay = false;
        private bool _disposed = false;

        #endregion

        #region Properties

        public ObservableCollection<RomsBasePathItem> RomsBasePaths { get; } = [];

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string LoadingMessage
        {
            get => _loadingMessage;
            set => this.RaiseAndSetIfChanged(ref _loadingMessage, value);
        }

        public int LoadingProgress
        {
            get => _loadingProgress;
            set => this.RaiseAndSetIfChanged(ref _loadingProgress, value);
        }

        public int TotalPlatforms
        {
            get => _totalPlatforms;
            set => this.RaiseAndSetIfChanged(ref _totalPlatforms, value);
        }

        public bool CanSave => !IsLoading;

        public ObservableCollection<PlatformNameSetting> PlatformNames { get; } = [];
        public List<PlatformOption> SharedAvailablePlatforms => _cachedAvailablePlatforms ?? [];

        #endregion

        #region Events

        public event Action? BackRequested;

        #endregion

        #region Constructor

        public RomSettingViewModel()
        {
            IsLoading = true;
            LoadingMessage = "초기화중...";

            var settings = SettingsService.LoadSettings();

            _cachedConfig = CommandConfigServiceFactory.Create?.Invoke()?.LoadConfig();
            _cachedAvailablePlatforms = GetAvailablePlatforms();

            foreach (var path in settings.RomsBasePaths)
                RomsBasePaths.Add(new RomsBasePathItem { Path = path });

            if (RomsBasePaths.Count == 0)
                RomsBasePaths.Add(new RomsBasePathItem());
        }

        public async Task InitializeAsync() => await LoadPlatformNamesAsync();

        #endregion

        #region Platform Loading

        private async Task LoadPlatformNamesAsync()
        {
            if (_isCurrentlyLoading) return;
            _isCurrentlyLoading = true;

            try
            {
                var savedSettings = SettingsService.LoadSettings();
                var mappingConfig = PlatformMappingService.Instance.LoadMapping();
                var savedPlatformSettings = savedSettings.PlatformSettings ?? [];
                var scanner = FolderScannerFactory.Create?.Invoke();
                var converter = PathConverterFactory.Create?.Invoke();

                var allFolders = new List<(string BasePath, string Name, string Path, bool IsNew)>();

                foreach (var basePath in RomsBasePaths)
                {
                    if (string.IsNullOrEmpty(basePath.Path)) continue;

                    var realPath = converter?.FriendlyPathToRealPath(basePath.Path) ?? basePath.Path;

                    if (scanner == null || string.IsNullOrEmpty(realPath))
                    {
                        var settingsForThisBase = savedPlatformSettings
                            .Where(kvp => kvp.Value.BasePath == basePath.Path)
                            .Select(kvp => (basePath.Path, kvp.Value.Name, "[경로 없음]", false));

                        allFolders.AddRange(settingsForThisBase);
                    }
                    else
                    {
                        try
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                IsLoading = true;
                                LoadingMessage = $"폴더 검색중: {basePath.Path}";
                            });

                            var scannedFolders = await Task.Run(() =>
                            {
                                if (!Directory.Exists(realPath))
                                    return [];

                                return Directory.GetDirectories(realPath)
                                    .Select(d => (Name: Path.GetFileName(d)!, FullPath: d))
                                    .ToArray();
                            });

                            foreach (var (Name, FullPath) in scannedFolders)
                            {
                                var compositeKey = Path.Combine(basePath.Path, Name);
                                var isNew = !savedPlatformSettings.ContainsKey(compositeKey);

                                allFolders.Add((basePath.Path, Name, Name, isNew));
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }

                var validBasePaths = new HashSet<string>(RomsBasePaths.Where(bp => !string.IsNullOrEmpty(bp.Path)).Select(bp => bp.Path));

                foreach (var saved in savedPlatformSettings)
                {
                    var compositeKey = saved.Key;

                    string savedBasePath;
                    string savedFolderName;

                    var foundBasePath = validBasePaths
                        .Where(bp => compositeKey.StartsWith(bp, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(bp => bp.Length)
                        .FirstOrDefault();

                    if (foundBasePath != null)
                    {
                        savedBasePath = foundBasePath;
                        savedFolderName = compositeKey[foundBasePath.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    }
                    else continue;

                    var alreadyScanned = allFolders.Any(f => f.BasePath.Equals(savedBasePath, StringComparison.OrdinalIgnoreCase) && f.Name.Equals(savedFolderName, StringComparison.OrdinalIgnoreCase));

                    if (validBasePaths.Contains(savedBasePath) && !alreadyScanned) allFolders.Add((savedBasePath, savedFolderName, "[경로 없음]", false));
                }

                var platformCount = allFolders.Count;
                _useLoadingOverlay = platformCount > LOADING_OVERLAY_THRESHOLD;

                if (_useLoadingOverlay) await ShowLoadingOverlayAsync(platformCount);

                var tempList = await LoadPlatformsOptimizedAsync(allFolders, mappingConfig);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PlatformNames.Clear();

                    foreach (var item in tempList) PlatformNames.Add(item);
                });
            }
            finally
            {
                _isCurrentlyLoading = false;

                if (_useLoadingOverlay)
                    await HideLoadingOverlayAsync();
                else
                {
                    IsLoading = false;
                    this.RaisePropertyChanged(nameof(CanSave));
                }
            }
        }

        private async Task ShowLoadingOverlayAsync(int totalCount)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = true;
                LoadingProgress = 0;
                LoadingMessage = "폴더 스캔중...";
                TotalPlatforms = totalCount;
                this.RaisePropertyChanged(nameof(CanSave));
            });
        }

        private async Task HideLoadingOverlayAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsLoading = false;
                LoadingProgress = 0;
                LoadingMessage = string.Empty;
                this.RaisePropertyChanged(nameof(CanSave));
            });
        }

        private async Task<List<PlatformNameSetting>> LoadPlatformsOptimizedAsync(List<(string BasePath, string Name, string Path, bool IsNew)> folders, PlatformMappingConfig mappingConfig)
        {
            return await Task.Run(() =>
            {
                var result = new List<PlatformNameSetting>();
                var converter = PathConverterFactory.Create?.Invoke();

                foreach (var folder in folders)
                {
                    try
                    {
                        var compositeKey = Path.Combine(folder.BasePath, folder.Name);

                        var realPath = converter?.FriendlyPathToRealPath(compositeKey) ?? compositeKey;
                        var mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(realPath) ?? TryAutoMapPlatform(folder.Name);
                        var normalizedMappedId = !string.IsNullOrEmpty(mappedPlatformId) ? PlatformInfoService.NormalizePlatformId(mappedPlatformId) : null;
                        var selectedOption = _cachedAvailablePlatforms?.FirstOrDefault(p => PlatformInfoService.NormalizePlatformId(p.Id) == normalizedMappedId);
                        var customDisplayName = mappingConfig?.CustomDisplayNames?.TryGetValue(compositeKey, out var displayName) == true ? displayName : null;

                        result.Add(new PlatformNameSetting
                        {
                            BasePath = folder.BasePath,
                            FolderName = folder.Name,
                            ActualPath = folder.Path,
                            IsNew = folder.IsNew,
                            SelectedPlatformOption = selectedOption,
                            CustomDisplayName = customDisplayName,
                            AvailablePlatforms = _cachedAvailablePlatforms ?? []
                        });
                    }
                    catch { }
                }

                return result.OrderBy(p => p.BasePath).ThenBy(p => p.FolderName).ToList();
            });
        }

        #endregion

        #region Platform Helpers

        private List<PlatformOption> GetAvailablePlatforms()
        {
            if (_cachedConfig == null) return [];

            var platforms = new HashSet<string>();

            foreach (var emulator in _cachedConfig.Emulators.Values)
                foreach (var platformId in emulator.SupportedPlatforms)
                    platforms.Add(platformId);

            return [.. platforms
                .OrderBy(x => x)
                .Select(id => new PlatformOption
                {
                    Id = id,
                    DisplayName = PlatformInfoService.GetPlatformDisplayName(id),
                    Image = LoadPlatformThumbnail(id)
                })];
        }

        private static Bitmap? LoadPlatformThumbnail(string platformId)
        {
            try
            {
                var info = PlatformInfoService.GetPlatformInfo(platformId);
                var uri = new Uri(ResourceHelper.GetPlatformImage(info.Id));

                if (!AssetLoader.Exists(uri)) return null;

                using var stream = AssetLoader.Open(uri);
                var originalBitmap = new Bitmap(stream);
                const int thumbnailSize = 32;
                var resized = originalBitmap.CreateScaledBitmap(new Avalonia.PixelSize(thumbnailSize, thumbnailSize));

                originalBitmap.Dispose();

                return resized;
            }
            catch
            {
                return null;
            }
        }

        private string? TryAutoMapPlatform(string folderName)
        {
            if (_cachedConfig == null) return null;

            var extractedId = PlatformInfoService.ExtractPlatformIdFromFolderName(folderName);

            foreach (var emulator in _cachedConfig.Emulators.Values)
            {
                foreach (var supportedPlatform in emulator.SupportedPlatforms)
                {
                    var normalizedSupported = PlatformInfoService.NormalizePlatformId(supportedPlatform);

                    if (normalizedSupported.Equals(extractedId, StringComparison.OrdinalIgnoreCase))
                        return normalizedSupported;
                }
            }

            return null;
        }

        #endregion

        #region Actions

        public void DeletePlatform(PlatformNameSetting? platform = null)
        {
            if (platform != null)
                PlatformNames.Remove(platform);
        }

        public async Task SaveSettingsAsync()
        {
            if (IsLoading) return;

            await Task.Run(() =>
            {
                var settings = SettingsService.LoadSettings();
                settings.PlatformSettings.Clear();
                settings.RomsBasePaths.Clear();

                var validBasePaths = RomsBasePaths
                    .Where(bp => !string.IsNullOrEmpty(bp.Path))
                    .Select(bp => bp.Path)
                    .ToList();

                foreach (var basePath in validBasePaths)
                    settings.RomsBasePaths.Add(basePath);

                if (settings.RomsBasePaths.Count > 0)
                    PathHelper.Initialize(settings.RomsBasePaths);

                foreach (var p in PlatformNames)
                {
                    if (string.IsNullOrEmpty(p.BasePath)) continue;
                    var compositeKey = Path.Combine(p.BasePath, p.FolderName);

                    settings.PlatformSettings[compositeKey] = new PlatformSettings
                    {
                        Name = p.FolderName,
                        BasePath = p.BasePath,
                        ImagePath = null
                    };
                }

                SettingsService.SavePlatformSettings(settings);

                var mappingConfig = new PlatformMappingConfig
                {
                    FolderMappings = [],
                    CustomDisplayNames = []
                };

                foreach (var platform in PlatformNames)
                {
                    var compositeKey = Path.Combine(platform.BasePath, platform.FolderName);

                    if (!string.IsNullOrEmpty(platform.SelectedPlatform))
                    {
                        var normalized = PlatformInfoService.NormalizePlatformId(platform.SelectedPlatform);
                        mappingConfig.FolderMappings[compositeKey] = normalized;
                    }

                    if (!string.IsNullOrEmpty(platform.CustomDisplayName))
                        mappingConfig.CustomDisplayNames[compositeKey] = platform.CustomDisplayName;
                }

                PlatformMappingService.Instance.SaveMapping(mappingConfig);

                MetadataService.ClearCache();
            });

            PlatformMappingService.Instance.ClearCache();
            AllGamesManager.Instance.Clear();

            var currentSettings = SettingsService.LoadSettings();
            if (currentSettings.PlatformSettings.Count > 0)
            {
                var platformKeys = currentSettings.PlatformSettings.Keys.ToList();
                _ = Task.Run(() => MetadataService.PreloadAllPlatforms(platformKeys));
            }
        }

        public void AddBasePath() => RomsBasePaths.Add(new RomsBasePathItem());

        public void RemoveBasePath(RomsBasePathItem item)
        {
            if (RomsBasePaths.Count > 1)
            {
                var platformsToRemove = PlatformNames
                    .Where(p => NormalizePath(p.BasePath).Equals(NormalizePath(item.Path), StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var platform in platformsToRemove)
                    PlatformNames.Remove(platform);

                RomsBasePaths.Remove(item);
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.TrimEnd('\\', '/');
        }

        public void SetBasePath(string path, RomsBasePathItem? targetItem = null)
        {
            if (targetItem != null)
                targetItem.Path = path;
            else if (RomsBasePaths.Count > 0)
                RomsBasePaths[0].Path = path;
            else
                RomsBasePaths.Add(new RomsBasePathItem { Path = path });

            _ = LoadPlatformNamesAsync();
        }

        public void GoBack() => BackRequested?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            if (_cachedAvailablePlatforms != null)
            {
                foreach (var platform in _cachedAvailablePlatforms)
                    platform.Image?.Dispose();

                _cachedAvailablePlatforms.Clear();
            }

            foreach (var setting in PlatformNames)
                foreach (var platform in setting.AvailablePlatforms)
                    platform.Image?.Dispose();

            PlatformNames.Clear();
        }

        #endregion
    }
}