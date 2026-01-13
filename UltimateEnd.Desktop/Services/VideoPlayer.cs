using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DirectShowLib;
using UltimateEnd.Desktop.Controls;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class VideoPlayer : IVideoPlayer, IDisposable
    {
        private static IGraphBuilder? _graphBuilder;
        private static IMediaControl? _mediaControl;
        private static IMediaSeeking? _mediaSeeking;
        private static IVMRWindowlessControl9? _windowlessControl;
        private static IBaseFilter? _vmr9Filter;
        private static readonly Lock _lock = new();
        private static string? _lastVideoPath;
        private static string? _playingVideoPath;
        private static IntPtr _staticVideoWindowHandle;
        private static int _staticTargetWidth;
        private static int _staticTargetHeight;

        private static CancellationTokenSource? _currentCts;
        private static int _operationId;

        private IntPtr _videoWindowHandle;
        private int _targetWidth;
        private int _targetHeight;
        private bool _isDisposed;

        public object? GetPlayerInstance() => this;

        public int VideoWidth
        {
            get
            {
                if (_windowlessControl == null) return 0;
                try
                {
                    _windowlessControl.GetNativeVideoSize(out int width, out int height, out _, out _);
                    return width;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public int VideoHeight
        {
            get
            {
                if (_windowlessControl == null) return 0;
                try
                {
                    _windowlessControl.GetNativeVideoSize(out int width, out int height, out _, out _);
                    return height;
                }
                catch
                {
                    return 0;
                }
            }
        }

        public void SetVideoWindow(IntPtr hwnd, int width, int height)
        {
            _videoWindowHandle = hwnd;
            _targetWidth = width;
            _targetHeight = height;

            _staticVideoWindowHandle = hwnd;
            _staticTargetWidth = width;
            _staticTargetHeight = height;
        }

        public void Play(string videoPath)
        {
            if (string.IsNullOrEmpty(videoPath) || _videoWindowHandle == IntPtr.Zero)
                return;

            if (videoPath == _lastVideoPath)
                return;

            var currentOpId = Interlocked.Increment(ref _operationId);

            _currentCts?.Cancel();
            _currentCts = new CancellationTokenSource();
            var cts = _currentCts;

            _lastVideoPath = videoPath;

            Task.Run(() => PlayInternal(videoPath, cts.Token), cts.Token);
        }

        private static void PlayInternal(string videoPath, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return;

            lock (_lock)
            {
                if (ct.IsCancellationRequested)
                    return;

                if (videoPath == _playingVideoPath)
                    return;

                try
                {
                    CleanupGraph();

                    if (ct.IsCancellationRequested)
                        return;

                    _graphBuilder = (IGraphBuilder)new FilterGraph();
                    _mediaControl = (IMediaControl)_graphBuilder;
                    _mediaSeeking = (IMediaSeeking)_graphBuilder;

                    if (ct.IsCancellationRequested)
                    {
                        CleanupGraph();
                        return;
                    }

                    var vmr9 = new VideoMixingRenderer9();
                    _vmr9Filter = (IBaseFilter)vmr9;
                    _graphBuilder.AddFilter(_vmr9Filter, "VMR9");

                    var config = _vmr9Filter as IVMRFilterConfig9;
                    config?.SetRenderingMode(VMR9Mode.Windowless);

                    _windowlessControl = _vmr9Filter as IVMRWindowlessControl9;
                    if (_windowlessControl != null)
                    {
                        _windowlessControl.SetVideoClippingWindow(_staticVideoWindowHandle);
                        _windowlessControl.SetAspectRatioMode(VMR9AspectRatioMode.LetterBox);
                        _windowlessControl.SetBorderColor(0);
                    }

                    VideoHost.ShowWindow(_staticVideoWindowHandle, 0);

                    if (ct.IsCancellationRequested)
                    {
                        CleanupGraph();
                        return;
                    }

                    var hr = _graphBuilder.RenderFile(videoPath, null);

                    if (ct.IsCancellationRequested)
                    {
                        CleanupGraph();
                        return;
                    }

                    if (hr >= 0)
                    {
                        _playingVideoPath = videoPath;

                        SetVideoSize(_staticTargetWidth, _staticTargetHeight);
                        VideoHost.ShowWindow(_staticVideoWindowHandle, 5);
                        _mediaControl?.Run();
                    }
                }
                catch (Exception)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        CleanupGraph();
                    }
                }
            }
        }

        public static void SetVideoSize(int width, int height)
        {
            _staticTargetWidth = width;
            _staticTargetHeight = height;

            if (_windowlessControl != null)
            {
                try
                {
                    _windowlessControl.SetVideoPosition(null, new DsRect(0, 0, width, height));
                }
                catch { }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _lastVideoPath = null;
                _playingVideoPath = null;

                try
                {
                    _mediaControl?.Stop();
                }
                catch { }
            }
        }

        private static void CleanupGraph()
        {
            try
            {
                if (_mediaControl != null)
                {
                    try { _mediaControl.Stop(); }
                    catch { }
                }

                _mediaSeeking = null;
                _windowlessControl = null;

                if (_graphBuilder != null)
                {
                    try
                    {
                        if (_graphBuilder.EnumFilters(out IEnumFilters enumFilters) == 0)
                        {
                            IBaseFilter[] filters = new IBaseFilter[1];
                            while (enumFilters.Next(1, filters, IntPtr.Zero) == 0)
                            {
                                if (filters[0] != null)
                                {
                                    try
                                    {
                                        _graphBuilder.RemoveFilter(filters[0]);
                                        Marshal.ReleaseComObject(filters[0]);
                                    }
                                    catch { }
                                }
                            }
                            Marshal.ReleaseComObject(enumFilters);
                        }
                    }
                    catch { }
                }

                if (_vmr9Filter != null)
                {
                    try { Marshal.ReleaseComObject(_vmr9Filter); }
                    catch { }
                    _vmr9Filter = null;
                }

                if (_mediaControl != null)
                {
                    try { Marshal.ReleaseComObject(_mediaControl); }
                    catch { }
                    _mediaControl = null;
                }

                if (_graphBuilder != null)
                {
                    try { Marshal.ReleaseComObject(_graphBuilder); }
                    catch { }
                    _graphBuilder = null;
                }
            }
            catch { }
        }

        public void Pause()
        {
            lock (_lock)
            {
                try
                {
                    _mediaControl?.Pause();
                }
                catch { }
            }
        }

        public void ReleaseMedia()
        {
            _currentCts?.Cancel();

            lock (_lock)
            {
                _lastVideoPath = null;
                _playingVideoPath = null;

                CleanupGraph();
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _currentCts?.Cancel();
            CleanupGraph();
            GC.SuppressFinalize(this);
        }
    }
}