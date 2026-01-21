using System;
using System.Threading.Tasks;
using ReactiveUI;
using System.Reactive;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using Avalonia.Threading;

namespace UltimateEnd.Coordinators
{
    public class VideoPlaybackCoordinator : IDisposable
    {
        private readonly VideoPlayerManager _videoManager;
        private bool _isVideoContainerVisible = true;
        private bool _disposed;

        public object? MediaPlayer => _videoManager.PlayerInstance;

        public bool IsVideoContainerVisible
        {
            get => _isVideoContainerVisible;
            set
            {
                _isVideoContainerVisible = value;
                VideoContainerVisibilityChanged?.Invoke(value);
            }
        }

        public ReactiveCommand<GameMetadata, Unit> PlayInitialVideoCommand { get; set; }

        public event Action<bool>? VideoContainerVisibilityChanged;

        public VideoPlaybackCoordinator()
        {
            _videoManager = VideoPlayerManager.Instance;
            PlayInitialVideoCommand = ReactiveCommand.Create<GameMetadata>(ExecutePlayInitialVideo);
        }

        private void ExecutePlayInitialVideo(GameMetadata game)
        {
            if (game?.HasVideo == true)
                _ = _videoManager.PlayWithDelayAsync(game);
        }

        public void HandleSelectedGameChanged(GameMetadata? game)
        {
            if (game?.HasVideo == true)
                Dispatcher.UIThread.Post(() => _ = _videoManager.PlayWithDelayAsync(game), DispatcherPriority.Background);
            else
                _videoManager.Stop();

            if (game?.HasVideo == true)
                Dispatcher.UIThread.Post(() => _ = _videoManager.PlayWithDelayAsync(game), DispatcherPriority.Background);
            else
                _videoManager.Stop();
        }

        public void Stop()
        {
            _videoManager.Stop();

            Dispatcher.UIThread.Post(() => _videoManager.Stop(), DispatcherPriority.Background);
        }

        public async Task ResumeAsync(GameMetadata? game)
        {
            if (game?.HasVideo == true)
                await _videoManager.PlayWithDelayAsync(game);
        }

        public async Task ResumeIfAvailable(GameMetadata game)
        {
            if (game?.HasVideo == true)
                await _videoManager.PlayWithDelayAsync(game);
            else
                _videoManager.Stop();
        }

        public void CancelDelay() => _videoManager.CancelDelay();

        public void CancelScheduledPlayback() => _videoManager.CancelDelay();

        public void ReleaseMedia()
        {
            _videoManager.CancelDelay();
            _videoManager.ReleaseMedia();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _videoManager.Stop();
        }

        public void ForceStop()
        {
            _videoManager.CancelDelay();

            _videoManager.Stop();

            _videoManager.ReleaseMedia();

            Dispatcher.UIThread.Post(() =>
            {
                _videoManager.Stop();
            }, DispatcherPriority.Send);
        }
    }
}