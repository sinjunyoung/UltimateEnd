using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Services;
using UltimateEnd.Updater;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public partial class MainContentView : UserControl
    {
        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public MainContentView()
        {
            InitializeComponent();

            DialogService.Instance.RegisterMessageOverlay((title, message, type) => MessageOverlay.ShowMessage(title, message, type));
            DialogService.Instance.RegisterConfirmOverlay((title, message) => MessageOverlay.ShowConfirm(title, message));
            DialogService.Instance.RegisterThreeButtonOverlay((title, message, btn1, btn2, btn3) => MessageOverlay.ShowThreeButton(title, message, btn1, btn2, btn3));
            DialogService.Instance.RegisterLoadingOverlay(
                showLoading: (message, cts) =>
                {
                    Dispatcher.UIThread.Post(() => LoadingOverlay.Show(message, cts));
                    return Task.CompletedTask;
                },
                hideLoading: () =>
                {
                    Dispatcher.UIThread.Post(() => LoadingOverlay.Hide());
                    return Task.CompletedTask;
                },
                updateLoading: (message) =>
                {
                    Dispatcher.UIThread.Post(() => LoadingOverlay.UpdateMessage(message));
                    return Task.CompletedTask;
                }
            );

            this.AddHandler(KeyDownEvent, OnInputDetected, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
            this.AddHandler(PointerPressedEvent, OnInputDetected, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, true);
        }

        protected async override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (await NetworkHelper.IsInternetAvailableAsync())
            { 

                var updater = UpdaterFactory.Create();
                var manager = new UpdateManager(updater);

                var currentVersion = VersionHelper.GetCurrentVersion();
                var latestVersion = await manager.GetLatestVersionAsync();

                if (latestVersion != null && VersionHelper.IsNewerVersion(currentVersion, latestVersion))
                {
                    bool result = await DialogService.Instance.ShowConfirm($"새 버전이 있습니다: {latestVersion}\n업데이트 하시겠습니까?", "업데이트");

                    if (result)
                    {
                        var progress = new Progress<UpdateProgress>(p =>
                        {
                            var percentage = (int)(p.Progress * 100);
                            var message = $"{p.Status}\n{p.Details}\n{percentage}%";
                            DialogService.Instance.UpdateLoading(message);
                        });

                        await DialogService.Instance.ShowLoading("업데이트 준비 중...");

                        try
                        {
                            await manager.CheckAndUpdateAsync(progress);
                        }
                        catch (Exception ex)
                        {
                            await DialogService.Instance.ShowMessage("업데이트 실패", ex.Message, MessageType.Error);
                        }
                        finally
                        {
                            await DialogService.Instance.HideLoading();
                        }
                    }
                }
            }
        }

        public void ShowLoading(string message = "로딩 중...") => LoadingOverlay.Show(message);

        public void HideLoading() => LoadingOverlay.Hide();

        public bool IsBatchScrapVisible() => BatchScrapOverlay.Visible;

        public bool IsBatchScrapInProgress() => BatchScrapOverlay.IsScrapInProgress;

        private void OnInputDetected(object? sender, RoutedEventArgs e) => MainViewModel.OnUserInteractionDetected();
    }
}