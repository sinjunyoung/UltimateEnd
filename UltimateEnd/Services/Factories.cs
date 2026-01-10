using Avalonia.Platform.Storage;
using System;
using UltimateEnd.SaveFile;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Services
{
    public static class AppBaseFolderProviderFactory
    {
        public static Func<IAppBaseFolderProvider>? Create { get; set; }
    }

    public static class AppIconProviderFactory
    {
        public static Func<IAppIconProvider>? Create { get; set; }
    }

    public static class AppLifetimeFactory
    {
        public static Func<IAppLifetime>? Create { get; set; }
    }

    public static class AppProviderFactory
    {
        public static Func<IAppProvider?>? Create { get; set; }
    }

    public static class AssetPathProviderFactory
    {
        public static Func<IAssetPathProvider>? Create { get; set; }
    }

    public static class CommandConfigServiceFactory
    {
        public static Func<ICommandConfigService>? Create { get; set; }
    }

    public static class EmulatorSettingViewFactory
    {
        public static Func<EmulatorSettingViewModelBase>? Create { get; set; }
    }

    public static class EmulatorValidationHandlerFactory
    {
        public static Func<IEmulatorValidationHandler>? Create { get; set; }
    }

    public static class FileAccessorFactory
    {        
        public static Func<IFileAccessor>? Create { get; set; }
    }

    public static class FilePickerServiceFactory
    {
        public static Func<IStorageProvider, IFilePickerService>? Create { get; set; }
    }

    public static class FolderPickerFactory
    {
        public static Func<IFolderPicker>? Create { get; set; }
    }

    public static class FolderScannerFactory
    {
        public static Func<IFolderScanner>? Create { get; set; }
    }

    public static class GameLauncherFactory
    {
        public static Func<IGameLauncher>? Create { get; set; }
    }

    public static class GoogleOAuthFactory
    {
        public static Func<IGoogleOAuthService>? Create { get; set; }
    }    

    public static class KeyBindingSettingsViewFactory
    {
        public static Func<KeyBindingSettingsViewModelBase>? Create { get; set; }
    }

    public static class PathConverterFactory
    {
        public static Func<IPathConverter>? Create { get; set; }
    }

    public static class PathResolverFactory
    {
        public static Func<IPathResolver>? Create { get; set; }
    }

    public static class PathValidatorFactory
    {
        public static Func<IPathValidator>? Create { get; set; }
    }

    public static class PlatformServiceFactory
    {
        public static Func<IPlatformService>? Create { get; set; }
    }

    public static class PlatformStorageInfoFactory
    {
        public static Func<IPlatformStorageInfo>? Create { get; set; }
    }

    public static class SoundPlayerFactory
    {
        public static Func<ISoundPlayer>? Create { get; set; }
    }

    public static class StoragePathProviderFactory
    {
        public static Func<IStoragePathProvider>? Create { get; set; }
    }

    public static class TemplateVariableManagerFactory
    {
        public static Func<ITemplateVariableManager>? Create { get; set; }
    }
    
    public static class UiBehaviorFactory
    {
        public static Func<IUiBehavior>? Create { get; set; }
    }

    public static class VideoPlayerFactory
    {
        public static Func<IVideoPlayer>? CreateVideoPlayer { get; set; }
    }

    public static class VideoViewInitializerFactory
    {
        public static Func<IVideoViewInitializer>? Create { get; set; }
    }
}