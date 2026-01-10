using Avalonia.Controls;
using UltimateEnd.Android.Controls;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class VideoViewInitializer : IVideoViewInitializer
    {
        private VideoViewHost? _videoHost;

        public void Initialize(Panel videoContainer, object? mediaPlayer)
        {
            try
            {
                if (_videoHost != null)
                {
                    if (!videoContainer.Children.Contains(_videoHost))
                    {
                        videoContainer.Children.Clear();
                        videoContainer.Children.Add(_videoHost);
                    }

                    _videoHost.SetPlayer(mediaPlayer);
                    return;
                }

                _videoHost = new VideoViewHost();
                videoContainer.Children.Clear();
                videoContainer.Children.Add(_videoHost);
                _videoHost.SetPlayer(mediaPlayer);
            }
            catch { }
        }

        public void Cleanup(Panel videoContainer) { }
    }
}