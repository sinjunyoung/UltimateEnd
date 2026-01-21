using Android.Content;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Threading.Tasks;
using UltimateEnd.Android.Models;
using UltimateEnd.Android.Views.Overlays;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Views.Overlays;
using Avalonia.Controls;
using System.IO;

namespace UltimateEnd.Android.Services
{
    public class AppProvider : IAppProvider
    {
        public string PlatformId => "android";

        public string PlatformName => "Android";

        public AppProvider() { }

        public async Task<NativeAppInfo> BrowseAppsAsync()
        {
            var tcs = new TaskCompletionSource<NativeAppInfo>();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var app = Avalonia.Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime;
                var mainView = app?.MainView;

                if (mainView is UserControl userControl && userControl.Content is Grid grid)
                {
                    var overlay = new AppPickerOverlay();
                    grid.Children.Add(overlay);

                    EventHandler<InstalledAppInfo>? handler = null;
                    EventHandler<HiddenEventArgs>? hiddenHandler = null;

                    handler = (s, selectedApp) =>
                    {
                        var systemAppsPath = AppSettings.SystemAppsPath;
                        var converter = PathConverterFactory.Create?.Invoke();
                        var realSystemAppsPath = converter?.FriendlyPathToRealPath(systemAppsPath) ?? systemAppsPath;
                        var platformPath = Path.Combine(realSystemAppsPath, PlatformId);

                        Directory.CreateDirectory(platformPath);

                        var safeFileName = string.Join("_", selectedApp.DisplayName.Split(Path.GetInvalidFileNameChars()));
                        var dummyFileName = $"{safeFileName}.android";
                        var dummyFilePath = Path.Combine(platformPath, dummyFileName);
                        var fileContent = $"{selectedApp.PackageName}|{selectedApp.ActivityName}";

                        File.WriteAllText(dummyFilePath, fileContent);

                        var result = new NativeAppInfo
                        {
                            Identifier = dummyFileName,
                            DisplayName = selectedApp.DisplayName,
                            ActivityName = selectedApp.ActivityName,
                            Icon = selectedApp.Icon
                        };

                        overlay.AppSelected -= handler;
                        overlay.Hidden -= hiddenHandler;
                        grid.Children.Remove(overlay);
                        tcs.SetResult(result);
                    };

                    hiddenHandler = (s, e) =>
                    {
                        if (e.State == HiddenState.Cancel)
                        {
                            overlay.AppSelected -= handler;
                            overlay.Hidden -= hiddenHandler;
                            grid.Children.Remove(overlay);
                            tcs.SetResult(null);
                        }
                    };

                    overlay.AppSelected += handler;
                    overlay.Hidden += hiddenHandler;
                    overlay.Show();
                }
            });

            return await tcs.Task;
        }

        public async Task LaunchAppAsync(GameMetadata game)
        {
            var activity = MainActivity.Instance;

            if (activity == null || activity.IsFinishing || activity.IsDestroyed) throw new InvalidOperationException("MainActivity를 사용할 수 없습니다.");

            var tcs = new TaskCompletionSource<bool>();
            activity.SetGameExitWaiter(tcs);

            try
            {
                var context = AndroidApplication.AppContext;
                var packageName = game.RomFile;
                var activityName = game.EmulatorId;

                Intent? intent;

                if (string.IsNullOrEmpty(activityName))
                {
                    intent = context.PackageManager?.GetLaunchIntentForPackage(packageName);

                    if (intent == null) throw new InvalidOperationException($"앱을 실행할 수 없습니다: {packageName}");

                    intent.AddFlags(ActivityFlags.NewTask);
                }
                else
                {
                    intent = new Intent(Intent.ActionMain);
                    intent.SetComponent(new ComponentName(packageName, activityName));
                    intent.AddFlags(ActivityFlags.NewTask);
                    intent.AddFlags(ActivityFlags.ClearTask | ActivityFlags.ClearTop | ActivityFlags.NoHistory);
                }

                activity.Window?.SetFlags(global::Android.Views.WindowManagerFlags.Secure, global::Android.Views.WindowManagerFlags.Secure);
                context.StartActivity(intent);

                await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("앱 실행이 취소되었습니다.");
            }
        }
    }
}