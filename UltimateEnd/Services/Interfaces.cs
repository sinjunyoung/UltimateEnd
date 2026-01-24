using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.SaveFile;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Services
{
    public interface IAppBaseFolderProvider
    {
        string GetAppBaseFolder();

        string GetPlatformsFolder();

        string GetSystemAppsFolder();
    }

    public interface IAppIconProvider
    {
        Bitmap GetAppIcon(string command);
    }

    public interface IAppLifetime
    {
        void Shutdown();
    }

    public interface IAppProvider
    {
        string PlatformId { get; }

        string PlatformName { get; }

        Task<NativeAppInfo> BrowseAppsAsync();

        Task LaunchAppAsync(GameMetadata game);
    }

    public interface IAssetPathProvider
    {
        public string GetAssetPath(string subFolder, string fileName);
    }

    public interface ICommandConfig
    {
        Dictionary<string, IEmulatorCommand> EmulatorCommands { get; set; }

        Dictionary<string, IEmulatorCommand> Emulators { get; }

        Dictionary<string, string> DefaultEmulators { get; }

        void AddEmulator(IEmulatorCommand command);
    }

    public interface ICommandConfigService
    {
        ICommandConfig LoadConfig();

        void SaveConfig(ICommandConfig config);

        void ClearCache();
    }

    public interface IEmulatorCommand
    {
        string Id { get; }

        string Name { get; }

        bool IsRetroArch { get; }

        string? CoreName { get; }

        List<string> SupportedPlatforms { get; }

        string LaunchCommand { get; }

        Bitmap Icon { get; }
    }

    public interface IEmulatorValidationHandler
    {
        Task<EmulatorValidationAction> HandleValidationFailedAsync(EmulatorValidationResult validation);
    }

    public interface IFileAccessor
    {
        Stream? OpenRead(string path);

        bool Exists(string path);
    }

    public interface IFilePickerService
    {
        Task<string?> PickFileAsync(string title, string initialDirectory, FileFilterOptions filterOptions);

        List<FilePickerFileType> ProcessFileTypes(List<FilePickerFileType> fileTypes);
    }

    public interface IFolderPicker
    {
        Task<string?> PickFolderAsync(string title, string? defaultPath = null);
    }

    public interface IFolderScanner
    {
        List<FolderInfo> GetSubfolders(string path);
    }

    public interface IGameLauncher
    {
        Task LaunchGameAsync(GameMetadata game);

        Task<EmulatorValidationResult> ValidateEmulatorAsync(GameMetadata game);
    }

    public interface IGoogleOAuthService
    {
        Task<bool> AuthenticateAsync();

        Task<bool> TryAuthenticateFromStoredTokenAsync();

        string AccessToken { get; }

        bool IsAuthenticated { get; }
    }

    public interface IOverlay
    {
        void Show();

        void Hide(HiddenState state);

        bool Visible { get; }
    }

    public interface IPathConverter
    {
        string UriToFriendlyPath(string path);

        string FriendlyPathToUri(string path);

        string FriendlyPathToRealPath(string displayPath);

        string RealPathToFriendlyPath(string realPath);
    }

    public interface IPathResolver
    {
        string GetPath(IStorageFile file);
    }

    public interface IPathValidator
    {
        bool ValidatePath(string path);
    }   

    public interface IPlatformMapping
    {
        List<string> Emulators { get; }

        string? Default { get; set; }
    }

    public interface IPlatformService
    {
        string GetAppVersion();

        string GetAppName();
    }

    public interface IPlatformStorageInfo
    {
        string? GetPrimaryStoragePath();

        string? GetExternalSdCardPath();
    }

    public interface ISaveBackupService
    {
        Task<bool> BackupSaveAsync(GameMetadata game, SaveBackupMode mode = SaveBackupMode.NormalSave);

        Task<bool> RestoreSaveAsync(GameMetadata game, string fileId);

        Task<bool> RestoreSaveAsync(GameMetadata game, SaveBackupMode? mode = null);

        Task<bool> HasBackupAsync(GameMetadata game, SaveBackupMode? mode = null);

        Task<List<SaveBackupInfo>> GetBackupListAsync(GameMetadata game, int limit = 30);
    }

    public interface ISoundPlayer
    {
        Task PlayAsync(string filePath);
    }

    public interface IStoragePathProvider
    {
        string? GetDefaultRomsPath();
    }

    public interface ITemplateVariableManager
    {
        List<TemplateVariable> Variables { get; }
    }

    public interface IVideoPlayer : IDisposable
    {
        void Play(string videoPath);

        void Stop();

        void Pause();

        void ReleaseMedia();

        object? GetPlayerInstance();

        int VideoWidth { get; }

        int VideoHeight { get; }
    }

    public interface IVideoViewInitializer
    {
        void Initialize(Panel videoContainer, object? mediaPlayer);

        void Cleanup(Panel videoContainer);
    }
}