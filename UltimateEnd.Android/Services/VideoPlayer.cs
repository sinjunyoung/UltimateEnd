using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Common;
using Android.App;
using UltimateEnd.Services;
using System;
using System.Threading;

namespace UltimateEnd.Android.Services
{
    public class VideoPlayer : IVideoPlayer
    {
        private static IExoPlayer? _player;
        private static readonly Lock _lock = new();
        private static string? _lastVideoPath;
        private static bool _isInitialized = false;
        private bool _isDisposed = false;

        public int VideoWidth
        {
            get
            {
                if (_player == null) return 0;
                try
                {
                    var videoSize = _player.VideoSize;
                    if (videoSize != null && videoSize.Width > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Got VideoSize.Width: {videoSize.Width}");
                        return videoSize.Width;
                    }

                    var format = _player.VideoFormat;
                    if (format != null && format.Width > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Got Format.Width: {format.Width}");
                        return format.Width;
                    }

                    System.Diagnostics.Debug.WriteLine("VideoWidth: No size available");
                    return 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"VideoWidth exception: {ex}");
                    return 0;
                }
            }
        }

        public int VideoHeight
        {
            get
            {
                if (_player == null) return 0;
                try
                {
                    var videoSize = _player.VideoSize;
                    if (videoSize != null && videoSize.Height > 0)
                    {
                        return videoSize.Height;
                    }

                    var format = _player.VideoFormat;
                    if (format != null && format.Height > 0)
                    {
                        return format.Height;
                    }

                    return 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public VideoPlayer()
        {
            lock (_lock)
            {
                if (!_isInitialized)
                {
                    try
                    {
                        var context = Application.Context;
                        _player = new ExoPlayerBuilder(context).Build();
                        _isInitialized = true;
                    }
                    catch { }
                }
            }
        }

        public object? GetPlayerInstance() => _player;

        public void Play(string videoPath)
        {
            if (_isDisposed || _player == null || string.IsNullOrEmpty(videoPath))
                return;

            lock (_lock)
            {
                if (videoPath == _lastVideoPath)
                    return;

                _lastVideoPath = videoPath;

                try
                {
                    var uri = global::Android.Net.Uri.Parse(videoPath);
                    var mediaItem = MediaItem.FromUri(uri);
                    _player?.SetMediaItem(mediaItem);
                    _player?.Prepare();
                    _player?.Play();
                }
                catch { }
            }
        }

        public void Pause()
        {
            if (_isDisposed || _player == null) return;

            lock (_lock)
            {
                try
                {
                    Application.SynchronizationContext?.Post(_ =>
                    {
                        try
                        {
                            _player?.Pause();
                        }
                        catch { }
                    }, null);
                }
                catch { }
            }
        }

        public void Stop()
        {
            if (_isDisposed || _player == null) return;

            lock (_lock)
            {
                _lastVideoPath = null;

                try
                {
                    _player?.Stop();
                }
                catch { }
            }
        }

        public void ReleaseMedia()
        {
            if (_isDisposed || _player == null) return;

            lock (_lock)
            {
                _lastVideoPath = null;

                try
                {
                    _player?.Stop();
                    _player?.ClearMediaItems();
                }
                catch { }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }

        public static void ReleaseStaticResources()
        {
            lock (_lock)
            {
                try
                {
                    _player?.Release();
                    _player = null;
                    _isInitialized = false;
                    _lastVideoPath = null;
                }
                catch { }
            }
        }
    }
}