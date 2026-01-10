using Avalonia.Controls;
using Avalonia.Threading;
using UltimateEnd.Services;
using UltimateEnd.ViewModels;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Android.Services
{
    public class UiBehavior : IUiBehavior
    {
        public void BeginDescriptionEdit(GameListViewModel vm, Control root)
        {
            var videoContainer = root.FindControl<Panel>("VideoContainer");

            if (videoContainer != null)
                videoContainer.IsVisible = false;

            vm.IsEditingDescriptionOverlay = true;

            Dispatcher.UIThread.Post(() =>
            {
                var overlay = root.FindControl<DescriptionEditOverlay>("DescriptionEditOverlay");
                if (overlay != null)
                {
                    var textBox = overlay.FindControl<TextBox>("OverlayDescriptionTextBox");

                    if (textBox != null)
                    {
                        textBox.Text = vm.SelectedGame?.Description ?? string.Empty;
                        textBox.Focus();
                    }
                }
            }, DispatcherPriority.Render);
        }
    }
}