using System;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Managers
{
    public class VideoPlayerManager : IDisposable
    {
        private static VideoPlayerManager? _instance;
        private static readonly object _lock = new();

        private readonly IVideoPlayer? _videoPlayer;
        private CancellationTokenSource? _delayCts;
        private bool _disposed;
        private string? _lastVideoPath;

        public object? PlayerInstance => _videoPlayer?.GetPlayerInstance();

        public int VideoWidth => _videoPlayer?.VideoWidth ?? 0;

        public int VideoHeight => _videoPlayer?.VideoHeight ?? 0;

        private VideoPlayerManager() => _videoPlayer = VideoPlayerFactory.CreateVideoPlayer?.Invoke();

        public static VideoPlayerManager Instance
        {
            get
            {
                if (_instance == null)
                    lock (_lock)
                        _instance ??= new VideoPlayerManager();

                return _instance;
            }
        }

        public async Task PlayWithDelayAsync(GameMetadata? game, int delayMs = 100)
        {
            if (game == null || !game.HasVideo) return;

            _delayCts?.Cancel();
            _delayCts = new CancellationTokenSource();
            var token = _delayCts.Token;

            try
            {
                await Task.Delay(delayMs, token);

                if (!token.IsCancellationRequested)
                    Play(game);
            }
            catch (TaskCanceledException) { }
        }

        private void Play(GameMetadata? game)
        {
            if (_videoPlayer == null || game == null || !game.HasVideo)
                return;

            try
            {
                var videoPath = game.GetVideoPath();

                if (string.IsNullOrEmpty(videoPath))
                    return;

                if (videoPath == _lastVideoPath)
                    return;

                _videoPlayer.Stop();
                _lastVideoPath = videoPath;
                _videoPlayer.Play(videoPath);
            }
            catch { }
        }

        public void Stop()
        {
            if (_videoPlayer == null) return;

            _delayCts?.Cancel();
            _lastVideoPath = null;

            try
            {
                _videoPlayer.Stop();
            }
            catch { }
        }

        public void CancelDelay() => _delayCts?.Cancel();

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _delayCts?.Cancel();
            _delayCts?.Dispose();

            try
            {
                _videoPlayer?.Stop();
                _videoPlayer?.Dispose();
            }
            catch { }

            lock (_lock)
                _instance = null;
        }
    }
}