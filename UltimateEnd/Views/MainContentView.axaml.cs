using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Threading.Tasks;
using UltimateEnd.Services;
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

        public void ShowLoading(string message = "·Îµù Áß...") => LoadingOverlay.Show(message);

        public void HideLoading() => LoadingOverlay.Hide();

        public bool IsBatchScrapVisible() => BatchScrapOverlay.Visible;

        public bool IsBatchScrapInProgress() => BatchScrapOverlay.IsScrapInProgress;

        private void OnInputDetected(object? sender, RoutedEventArgs e) => MainViewModel.OnUserInteractionDetected();
    }
}