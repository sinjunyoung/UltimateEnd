using Android.Views;
using AndroidX.Media3.ExoPlayer;
using Avalonia.Controls;
using Avalonia.Platform;
using System;
using Application = global::Android.App.Application;

namespace UltimateEnd.Android.Controls
{
    public class VideoViewHost : NativeControlHost
    {
        private CustomPlayerView? _playerView;
        private bool _isDisposed;
        private IExoPlayer? _pendingPlayer;
        private bool _isNativeControlCreated;

        public VideoViewHost()
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            MinWidth = 100;
            MinHeight = 100;
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            try
            {
                var context = Application.Context;
                _playerView = new CustomPlayerView(context)
                {
                    LayoutParameters = new ViewGroup.LayoutParams(
                        ViewGroup.LayoutParams.MatchParent,
                        ViewGroup.LayoutParams.MatchParent
                    )
                };

                _isNativeControlCreated = true;

                if (_pendingPlayer != null)
                {
                    _playerView.Player = _pendingPlayer;
                    _pendingPlayer = null;
                }

                return new PlatformHandle(_playerView.Handle, "UltimateEnd.Android.Controls.CustomPlayerView");
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try
            {
                if (_playerView != null)
                {
                    Application.SynchronizationContext?.Post(_ =>
                    {
                        try
                        {
                            _playerView.Visibility = ViewStates.Gone;
                            _playerView.Alpha = 0.0f;
                        }
                        catch { }
                    }, null);

                    _playerView.Player = null;

                    _playerView.Dispose();
                    _playerView = null;
                }

                _pendingPlayer = null;
            }
            catch { }
        }

        public void SetPlayer(object? player)
        {
            if (_isDisposed) return;

            try
            {
                if (player is IExoPlayer exoPlayer)
                {
                    if (_isNativeControlCreated && _playerView != null)
                    {
                        _playerView.Player = exoPlayer;
                        _pendingPlayer = null;
                    }
                    else
                        _pendingPlayer = exoPlayer;
                }
                else if (player == null)
                {
                    _pendingPlayer = null;
                    if (_playerView != null)
                        _playerView.Player = null;
                }
            }
            catch { }
        }

        public bool HasPlayer() => _playerView?.Player != null || _pendingPlayer != null;
    }
}