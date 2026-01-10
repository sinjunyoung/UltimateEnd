using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using UltimateEnd.Desktop.Controls;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class VideoViewInitializer : IVideoViewInitializer
    {
        private VideoHost? _videoHost;
        private VideoPlayer? _player;

        public void Initialize(Panel videoContainer, object? mediaPlayer)
        {
            if (mediaPlayer is not VideoPlayer player) return;
            _player = player;

            if (_videoHost != null)
            {
                if (!videoContainer.Children.Contains(_videoHost))
                {
                    videoContainer.Children.Clear();
                    videoContainer.Children.Add(_videoHost);
                }
                UpdateLayoutInternal();
                return;
            }

            _videoHost = new VideoHost();
            _videoHost.SizeChanged += (s, e) => UpdateLayoutInternal();

            videoContainer.Children.Clear();
            videoContainer.Children.Add(_videoHost);

            Dispatcher.UIThread.Post(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    if (_videoHost.GetHandle() != IntPtr.Zero) break;
                    await Task.Delay(50);
                }
                UpdateLayoutInternal();
            }, DispatcherPriority.Loaded);
        }

        private void UpdateLayoutInternal()
        {
            if (_videoHost == null || _player == null) return;

            var topLevel = TopLevel.GetTopLevel(_videoHost);
            double scaling = topLevel?.RenderScaling ?? 1.0;
            int pWidth = (int)Math.Round(_videoHost.Bounds.Width * scaling);
            int pHeight = (int)Math.Round(_videoHost.Bounds.Height * scaling);

            IntPtr handle = _videoHost.GetHandle();
            if (handle != IntPtr.Zero && pWidth > 0 && pHeight > 0)
            {
                VideoHost.SetWindowPos(handle, IntPtr.Zero, 0, 0, pWidth, pHeight, 0x0014);
                _player.SetVideoWindow(handle, pWidth, pHeight);
                _player.SetVideoSize(pWidth, pHeight);
                _videoHost.ApplyRounding(pWidth, pHeight, (int)(16 * scaling));
            }
        }

        public void Cleanup(Panel videoContainer) { /* 호스트 재사용을 위해 비워둠 */ }
    }
}