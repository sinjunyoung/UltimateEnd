using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UltimateEnd.Android.SaveFile;
using UltimateEnd.Android.Services;
using UltimateEnd.Android.ViewModels;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using UltimateEnd.Views;

namespace UltimateEnd.Android;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppBaseFolderProviderFactory.Create = () => new AppBaseFolderProvider();
        AppIconProviderFactory.Create = () => new AppIconProvider();
        AppLifetimeFactory.Create = () => new AppLifetime();
        AppProviderFactory.Create = () => new AppProvider();
        AssetPathProviderFactory.Create = () => new AssetPathProvider();
        CommandConfigServiceFactory.Create = () => new CommandConfigService();
        EmulatorSettingViewFactory.Create = () => new EmulatorSettingViewModel();
        EmulatorValidationHandlerFactory.Create = () => new EmulatorValidationHandler();
        FileAccessorFactory.Create = () => new FileAccessor(AndroidApplication.AppContext);
        FilePickerServiceFactory.Create = (provider) => new FilePickerService(provider, AndroidApplication.AppContext);
        FolderPickerFactory.Create = () => new FolderPicker();
        FolderScannerFactory.Create = () => new FolderScanner();
        GameLauncherFactory.Create = () => new GameLauncher();
        GoogleOAuthFactory.Create = () => GoogleOAuthService.GetInstance(MainActivity.Instance);
        KeyBindingSettingsViewFactory.Create = () => new KeyBindingSettingsViewModel();
        PathConverterFactory.Create = () => new PathConverter(AndroidApplication.AppContext);
        PathResolverFactory.Create = () => new PathResolver(AndroidApplication.AppContext);
        PathValidatorFactory.Create = () => new PathValidator();
        PlatformServiceFactory.Create = () => new PlatformService();
        PlatformStorageInfoFactory.Create = () => new PlatformStorageInfo(AndroidApplication.AppContext);
        SaveBackupServiceFactoryProvider.Register(driveService => new SaveBackupServiceFactory(driveService));
        SoundPlayerFactory.Create = () => new SoundPlayer();
        StoragePathProviderFactory.Create = () => new StoragePathProvider();
        TemplateVariableManagerFactory.Create = () => new TemplateVariableManager();
        UiBehaviorFactory.Create = () => new UiBehavior();
        VideoPlayerFactory.CreateVideoPlayer = () => new VideoPlayer();
        VideoViewInitializerFactory.Create = () => new VideoViewInitializer();
        WavSounds.Initialize(AssetPathProviderFactory.Create.Invoke());

        if(ApplicationLifetime is ISingleViewApplicationLifetime singleView)
    {
            singleView.MainView ??= new MainContentView
                {
                    DataContext = new MainViewModel()
                };
        }

        ThemeService.Initialize();
        base.OnFrameworkInitializationCompleted();
    }
}