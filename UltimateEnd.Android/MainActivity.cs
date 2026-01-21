using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ReactiveUI.Avalonia;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UltimateEnd.Android.Dialogs;
using UltimateEnd.Android.SaveFile;
using UltimateEnd.Android.Services;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.SaveFile;
using UltimateEnd.Scraper;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using Keycode = Android.Views.Keycode;
using Uri = Android.Net.Uri;

namespace UltimateEnd.Android;

[Activity(
    Label = "UltimateEnd",
    Theme = "@style/MyTheme.NoActionBar.Fullscreen",
    Icon = "@drawable/icon",
    MainLauncher = true,
    Exported = true,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public class MainActivity : AvaloniaMainActivity<App>
{
    #region Constants and Static Properties

    const int STORAGE_PERMISSION_CODE = 1000;
    const int PICK_FOLDER_REQUEST = 1001;
    const int MANAGE_ALL_FILES_REQUEST_CODE = 1002;

    public static MainActivity? Instance { get; private set; }

    private TaskCompletionSource<bool>? _gameExitTcs;

    public Action<Uri?>? FolderPickerResult { get; set; }

    #endregion

    #region Fields

    private GoogleOAuthService _googleOAuth;
    private GoogleDriveService _googleDrive;

    #endregion

    #region Google Drive

    public void HandleOAuthRedirect(Uri uri) => _googleOAuth?.HandleRedirect(uri);

    private void InitializeGoogleDrive()
    {
        _googleOAuth = GoogleOAuthService.GetInstance(this);
        _googleDrive = new GoogleDriveService();
    }

    #endregion

    #region Overrides

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try { Process.SetThreadPriority(ThreadPriority.Display); } catch { }

        RequestWindowFeature(WindowFeatures.NoTitle);

        if (Window != null)
        {
            try
            {
                Window.SetSoftInputMode(SoftInput.AdjustResize);
                Window.SetFlags(WindowManagerFlags.HardwareAccelerated, WindowManagerFlags.HardwareAccelerated);
                Window.SetFlags(WindowManagerFlags.Fullscreen, WindowManagerFlags.Fullscreen);
                Window?.AddFlags(WindowManagerFlags.KeepScreenOn);

                SetImmersiveFullScreen();
            }
            catch { }
        }

        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                var builder = new StrictMode.VmPolicy.Builder();
                StrictMode.SetVmPolicy(builder.Build());
            }
        }
        catch { }

        RequestStoragePermissions();

        base.OnCreate(savedInstanceState);

        Instance = this;        

        try
        {
            Window?.DecorView?.SetOnSystemUiVisibilityChangeListener(new SystemUiVisibilityChangeListener(this));
        }
        catch { }

        EmulatorValidationHandler.CleanupOldApkFiles();

        InitializeGoogleDrive();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseReactiveUI()
            .UseSkia();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);

        if (hasFocus) Window?.DecorView?.PostDelayed(() => SetImmersiveFullScreen(), 100);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        if (requestCode == PICK_FOLDER_REQUEST && resultCode == Result.Ok && data?.Data != null)
        {
            var uri = data.Data;

            ContentResolver?.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

            FolderPickerResult?.Invoke(uri);
            FolderPickerResult = null;
        }
        else if (requestCode == MANAGE_ALL_FILES_REQUEST_CODE)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R && !global::Android.OS.Environment.IsExternalStorageManager)
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    var permissionsNeeded = new List<string>();

                    if (CheckSelfPermission(Manifest.Permission.ReadMediaImages) != Permission.Granted) permissionsNeeded.Add(Manifest.Permission.ReadMediaImages);
                    if (CheckSelfPermission(Manifest.Permission.ReadMediaVideo) != Permission.Granted) permissionsNeeded.Add(Manifest.Permission.ReadMediaVideo);
                    if (CheckSelfPermission(Manifest.Permission.ReadMediaAudio) != Permission.Granted) permissionsNeeded.Add(Manifest.Permission.ReadMediaAudio);
                    if (permissionsNeeded.Count > 0) RequestPermissions([.. permissionsNeeded], STORAGE_PERMISSION_CODE);
                }
            }
        }
    }

    protected override void OnPause()
    {
        ScreenScraperCache.FlushSync();

        base.OnPause();

        var app = Avalonia.Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime;

        if (app?.MainView?.DataContext is MainViewModel mainVm)
        {
            ScreenSaverManager.Instance.OnAppPaused();

            if (mainVm.CurrentView is GameListViewModel gameListVm) gameListVm.ForceStopVideo();
        }
    }

    public void SetGameExitWaiter(TaskCompletionSource<bool> tcs)
    {
        _gameExitTcs = tcs;
    }

    protected override async void OnResume()
    {
        base.OnResume();

        _gameExitTcs?.TrySetResult(true);
        _gameExitTcs = null;

        SetImmersiveFullScreen();

        await PlayTimeHistoryFactory.Instance.RecoverUnfinishedSessions();
        await PlayTimeHistoryFactory.Instance.StopAllActiveSessions();

        if (!AllGamesManager.Instance.IsLoaded) AllGamesManager.Instance.ResumeFullLoad();

        var app = Avalonia.Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime;

        if (app?.MainView?.DataContext is MainViewModel mainVm)
        {
            if (mainVm.CurrentView is GameListViewModel gameListVm)
            {
                await Dispatcher.UIThread.InvokeAsync(() => gameListVm.RefreshCurrentGamePlayHistory());

                if (FilePickerDialog.IsOpen || OverlayHelper.IsAnyOverlayVisible(app.MainView)) return;
                                
                gameListVm.ForceStopVideo();
                await Task.Delay(200);

                if (gameListVm.ViewMode == GameViewMode.List && !gameListVm.IsLaunchingGame)
                {
                    gameListVm.IsVideoContainerVisible = true;
                    gameListVm.EnableVideoPlayback();

                    await Task.Delay(50);
                    _ = gameListVm.ResumeVideoAsync();
                }
            }

            ScreenSaverManager.Instance.OnAppResumed();
        }
    }

    protected override void OnDestroy()
    {
        ScreenScraperCache.Shutdown();
        base.OnDestroy();
    }

    public override bool DispatchKeyEvent(KeyEvent? e)
    {
        if (e?.Action == KeyEventActions.Down)
        {
            var mappedKey = MapGamepadButton(e.KeyCode);

            if (mappedKey != Key.None)
            {
                SendAvaloniaKeyEvent(mappedKey, InputElement.KeyDownEvent);
                return true;
            }
        }

        if (e?.Action == KeyEventActions.Up)
        {
            var mappedKey = MapGamepadButton(e.KeyCode);

            if (mappedKey != Key.None)
            {
                SendAvaloniaKeyEvent(mappedKey, InputElement.KeyUpEvent);
                return true;
            }
        }

        return base.DispatchKeyEvent(e);
    }

    #endregion

    #region Private Methods

    private void SetImmersiveFullScreen()
    {
        var decorView = Window?.DecorView;
        if (decorView == null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            Window?.SetDecorFitsSystemWindows(false);
            var controller = Window?.InsetsController;

            if (controller != null)
            {
                controller.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
                controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
        }
        else
        {
            var uiOptions = (int)SystemUiFlags.HideNavigation | (int)SystemUiFlags.Fullscreen | (int)SystemUiFlags.ImmersiveSticky | (int)SystemUiFlags.LayoutStable | (int)SystemUiFlags.LayoutHideNavigation | (int)SystemUiFlags.LayoutFullscreen;

            decorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
        }
    }

    private void RequestStoragePermissions()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R) // Android 11+
        {
            if (!global::Android.OS.Environment.IsExternalStorageManager)
            {
                try
                {
                    var intent = new Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
                    intent.SetData(Uri.Parse($"package:{PackageName}"));
                    StartActivityForResult(intent, MANAGE_ALL_FILES_REQUEST_CODE);
                }
                catch
                {
                    var intent = new Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                    StartActivityForResult(intent, MANAGE_ALL_FILES_REQUEST_CODE);
                }
            }
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.M) // Android 6-10
        {
            if (CheckSelfPermission(Manifest.Permission.ReadExternalStorage) != Permission.Granted)
                RequestPermissions([Manifest.Permission.ReadExternalStorage], STORAGE_PERMISSION_CODE);
        }
    }

    #endregion

    #region Private Static Methods

    private static void SendAvaloniaKeyEvent(Key key, RoutedEvent routedEvent)
    {
        if (Dispatcher.UIThread.CheckAccess())
            RaiseKeyEvent(key, routedEvent);
        else
            Dispatcher.UIThread.Invoke(() => RaiseKeyEvent(key, routedEvent));
    }

    private static void RaiseKeyEvent(Key key, RoutedEvent routedEvent)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime lifetime)
        {
            var mainView = lifetime.MainView;
            var topLevel = TopLevel.GetTopLevel(mainView);

            var target = topLevel?.FocusManager?.GetFocusedElement() ?? mainView;

            target?.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = routedEvent,

                Key = key,
                Source = target,
            });
        }
    }

    private static Key MapGamepadButton(Keycode keyCode)
    {
        return keyCode switch
        {
            Keycode.ButtonA => Key.Enter,
            Keycode.ButtonB => Key.Escape,
            Keycode.ButtonX => Key.X,
            Keycode.ButtonY => Key.F,
            Keycode.ButtonL1 => Key.PageUp,
            Keycode.ButtonR1 => Key.PageDown,
            Keycode.ButtonStart => Key.F10,
            Keycode.ButtonSelect => Key.F11,
            Keycode.DpadUp => Key.Up,
            Keycode.DpadDown => Key.Down,
            Keycode.DpadLeft => Key.Left,
            Keycode.DpadRight => Key.Right,
            _ => Key.None
        };
    }

    #endregion

    public Task RunOnUiThreadAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        RunOnUiThread(() =>
        {
            try
            {
                action();
                tcs.TrySetResult(true);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    #region Nested Classes

    private class SystemUiVisibilityChangeListener(MainActivity activity) : Java.Lang.Object, View.IOnSystemUiVisibilityChangeListener
    {
        private readonly MainActivity _activity = activity;

        public void OnSystemUiVisibilityChange(StatusBarVisibility visibility) => _activity.SetImmersiveFullScreen();
    }

    #endregion

    #region GoogleOAuthActivity Class

    [Activity(Label = "OAuth", NoHistory = true, LaunchMode = global::Android.Content.PM.LaunchMode.SingleTop, Exported = true)]
    [Register("ultimateend.android.GoogleOAuthActivity")]
    [IntentFilter(
        [Intent.ActionView],
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "com.yamesoft.ultimateend",
        DataPath = "/oauth2redirect")]
    public class GoogleOAuthActivity : Activity
    {
        protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            var uri = Intent?.Data;

            if (uri != null) MainActivity.Instance?.HandleOAuthRedirect(uri);

            Finish();
        }
    }

    #endregion
}