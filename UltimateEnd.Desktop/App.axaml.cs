using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UltimateEnd.Desktop.SaveFile;
using UltimateEnd.Desktop.Services;
using UltimateEnd.Desktop.Utils;
using UltimateEnd.Desktop.ViewModels;
using UltimateEnd.SaveFile;
using UltimateEnd.Scraper;
using UltimateEnd.Services;
using UltimateEnd.Updater;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using UltimateEnd.Views;

namespace UltimateEnd.Desktop
{
    public class App : Application
    {
        private GamepadManager? _gamepadManager;

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
            FileAccessorFactory.Create = () => new FileAccessor();
            FilePickerServiceFactory.Create = (provider) => new FilePickerService(provider);
            FolderPickerFactory.Create = () => new FolderPicker();
            FolderScannerFactory.Create = () => new FolderScanner();
            GameLauncherFactory.Create = () => new GameLauncher();
            GoogleOAuthFactory.Create = () => new GoogleOAuthService();
            KeyBindingSettingsViewFactory.Create = () => new KeyBindingSettingsViewModel();
            PathConverterFactory.Create = () => new PathConverter();
            PathResolverFactory.Create = () => new PathResolver();
            PathValidatorFactory.Create = () => new PathValidator();
            PlatformServiceFactory.Create = () => new PlatformService();
            SaveBackupServiceFactoryProvider.Register(driveService => new SaveBackupServiceFactory(driveService));
            SoundPlayerFactory.Create = () => new SoundPlayer();            
            StoragePathProviderFactory.Create = () => new StoragePathProvider();
            TemplateVariableManagerFactory.Create = () => new TemplateVariableManager();
            VideoPlayerFactory.CreateVideoPlayer = () => new VideoPlayer();
            VideoViewInitializerFactory.Create = () => new VideoViewInitializer();
            WavSounds.Initialize(AssetPathProviderFactory.Create.Invoke());
            UpdaterFactory.Create = () => new Services.Updater();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow
                {
                    DataContext = new MainViewModel()
                };

                desktop.Exit += OnExit;
            }

            ThemeService.Initialize();            
            ScreenSaverBlocker.BlockWindowsScreenSaver();            

            try
            {
                _gamepadManager = new GamepadManager();
            }
            catch { }

            GamepadManager.GamepadConnectionChanged += () => InputManager.LoadKeyBindings();
            InputManager.LoadKeyBindings();

            base.OnFrameworkInitializationCompleted();
        }

        private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            ScreenScraperCache.Shutdown();
            _gamepadManager?.Dispose();
            ScreenSaverBlocker.RestoreWindowsScreenSaver();
        }
    }
}