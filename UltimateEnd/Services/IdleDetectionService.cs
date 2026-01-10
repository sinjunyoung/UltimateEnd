using Avalonia.Threading;
using System;

namespace UltimateEnd.Services
{
    public class IdleDetectionService : IDisposable
    {
        private readonly DispatcherTimer _idleTimer;
        private DateTime _lastActivityTime;
        private bool _isScreensaverActive;
        private bool _isEnabled;
        private TimeSpan _idleTimeout = TimeSpan.FromMinutes(5);

        public event Action? ScreensaverActivated;
        public event Action? UserActivityDetected;

        public TimeSpan IdleTimeout
        {
            get => _idleTimeout;
            set
            {
                _idleTimeout = value;

                if (value == TimeSpan.Zero)
                    Disable();
                else if (_isEnabled)
                    Enable();
            }
        }

        public bool IsScreensaverActive => _isScreensaverActive;

        public IdleDetectionService()
        {
            _lastActivityTime = DateTime.Now;
            _isEnabled = true;

            _idleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _idleTimer.Tick += OnTimerTick;
        }

        public void Start()
        {
            if (!_isEnabled || _idleTimeout == TimeSpan.Zero) return;

            _lastActivityTime = DateTime.Now;
            _idleTimer.Start();
        }

        public void Stop() => _idleTimer.Stop();

        public void Enable()
        {
            _isEnabled = true;
            _lastActivityTime = DateTime.Now;

            if (!_idleTimer.IsEnabled && _idleTimeout != TimeSpan.Zero)
                Start();
        }

        public void Disable()
        {
            _isEnabled = false;
            Stop();
            DeactivateScreensaver();
            _lastActivityTime = DateTime.Now;
        }

        public void ResetIdleTimer()
        {
            if (_isScreensaverActive)
            {
                DeactivateScreensaver();
                UserActivityDetected?.Invoke();
            }

            _lastActivityTime = DateTime.Now;
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (!_isEnabled || _idleTimeout == TimeSpan.Zero) return;

            var idleTime = DateTime.Now - _lastActivityTime;

            if (!_isScreensaverActive && idleTime >= _idleTimeout)
                ActivateScreensaver();
        }

        private void ActivateScreensaver()
        {
            if (_isScreensaverActive) return;

            _isScreensaverActive = true;
            ScreensaverActivated?.Invoke();
        }

        private void DeactivateScreensaver() => _isScreensaverActive = false;

        public void Dispose() => _idleTimer?.Stop();
    }
}