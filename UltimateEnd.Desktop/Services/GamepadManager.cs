using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using UltimateEnd.Desktop.Models;
using UltimateEnd.Enums;
using UltimateEnd.Utils;
using AKey = Avalonia.Input.Key;

namespace UltimateEnd.Desktop.Services
{
    public class GamepadManager : IDisposable
    {
        private readonly DirectInput _directInput = new();
        private readonly List<Joystick> _gamepads = [];
        private readonly DispatcherTimer _timer;
        private bool _isProcessing = false;

        private bool _prevDpadUp = false;
        private bool _prevDpadDown = false;
        private bool _prevDpadLeft = false;
        private bool _prevDpadRight = false;
        private DateTime _lastDpadMoveTime = DateTime.MinValue;
        private DateTime _dpadPressStartTime = DateTime.MinValue;

        private bool _prevStickUp = false;
        private bool _prevStickDown = false;
        private bool _prevStickLeft = false;
        private bool _prevStickRight = false;
        private DateTime _lastStickMoveTime = DateTime.MinValue;
        private DateTime _stickPressStartTime = DateTime.MinValue;
        private DateTime _lastStickDirectionChange = DateTime.MinValue;

        private readonly bool[] _prevButtons = new bool[128];

        private readonly TimeSpan InputInitialDelay = TimeSpan.FromMilliseconds(250);
        private readonly TimeSpan InputRepeatRate = TimeSpan.FromMilliseconds(50);
        private readonly TimeSpan StickDirectionChangeDelay = TimeSpan.FromMilliseconds(100);

        private const int StickDeadzone = 20000;
        private const int StickCenter = 32767;

        private AKey _cachedDPadUp;
        private AKey _cachedDPadDown;
        private AKey _cachedDPadLeft;
        private AKey _cachedDPadRight;
        private AKey _cachedButtonA;
        private AKey _cachedButtonB;
        private AKey _cachedButtonX;
        private AKey _cachedButtonY;
        private AKey _cachedLeftBumper;
        private AKey _cachedRightBumper;
        private AKey _cachedStart;
        private AKey _cachedSelect;

        public static Func<bool>? IsBindingActive { get; set; }

        public GamepadManager()
        {
            InitializeDevices();

            UpdateKeyMappingCache();

            InputManager.KeyMappingsChanged += OnKeyMappingsChanged;

            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (s, e) => PollInput());
            _timer.Start();
        }

        private void OnKeyMappingsChanged(object? sender, EventArgs e)
        {
            UpdateKeyMappingCache();
        }

        private void UpdateKeyMappingCache()
        {
            _cachedDPadUp = InputManager.GetMappedKey(GamepadButton.DPadUp);
            _cachedDPadDown = InputManager.GetMappedKey(GamepadButton.DPadDown);
            _cachedDPadLeft = InputManager.GetMappedKey(GamepadButton.DPadLeft);
            _cachedDPadRight = InputManager.GetMappedKey(GamepadButton.DPadRight);
            _cachedButtonA = InputManager.GetMappedKey(GamepadButton.ButtonA);
            _cachedButtonB = InputManager.GetMappedKey(GamepadButton.ButtonB);
            _cachedButtonX = InputManager.GetMappedKey(GamepadButton.ButtonX);
            _cachedButtonY = InputManager.GetMappedKey(GamepadButton.ButtonY);
            _cachedLeftBumper = InputManager.GetMappedKey(GamepadButton.LeftBumper);
            _cachedRightBumper = InputManager.GetMappedKey(GamepadButton.RightBumper);
            _cachedStart = InputManager.GetMappedKey(GamepadButton.Start);
            _cachedSelect = InputManager.GetMappedKey(GamepadButton.Select);
        }

        private void InitializeDevices()
        {
            var devices = _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

            foreach (var deviceInstance in devices)
            {
                try
                {
                    var joystick = new Joystick(_directInput, deviceInstance.InstanceGuid);
                    joystick.Properties.BufferSize = 128;
                    joystick.Acquire();
                    _gamepads.Add(joystick);
                }
                catch { }
            }
        }

        private void PollInput()
        {
            if (_gamepads.Count == 0 || _isProcessing) return;

            if (IsBindingActive?.Invoke() == true) return;

            try
            {
                _isProcessing = true;

                var joystick = _gamepads[0];
                joystick.Poll();
                var state = joystick.GetCurrentState();

                HandleDpad(state.PointOfViewControllers[0]);
                HandleLeftStick(state.X, state.Y);
                HandleButtons(state.Buttons);
            }
            catch { }
            finally
            {
                _isProcessing = false;
            }
        }

        private void HandleDpad(int povValue)
        {
            bool isUp = povValue == 0 || (povValue > 31500 && povValue < 36000) || (povValue >= 0 && povValue < 4500);
            bool isDown = povValue >= 13500 && povValue <= 22500;
            bool isLeft = povValue >= 22500 && povValue <= 31500;
            bool isRight = povValue >= 4500 && povValue <= 13500;

            var now = DateTime.Now;

            ProcessDirectionalInput(isUp, ref _prevDpadUp, _cachedDPadUp, ref _dpadPressStartTime, ref _lastDpadMoveTime, now);
            ProcessDirectionalInput(isDown, ref _prevDpadDown, _cachedDPadDown, ref _dpadPressStartTime, ref _lastDpadMoveTime, now);
            ProcessDirectionalInput(isLeft, ref _prevDpadLeft, _cachedDPadLeft, ref _dpadPressStartTime, ref _lastDpadMoveTime, now);
            ProcessDirectionalInput(isRight, ref _prevDpadRight, _cachedDPadRight, ref _dpadPressStartTime, ref _lastDpadMoveTime, now);
        }

        private void HandleLeftStick(int x, int y)
        {
            int deltaX = x - StickCenter;
            int deltaY = y - StickCenter;
            int absDeltaX = Math.Abs(deltaX);
            int absDeltaY = Math.Abs(deltaY);

            bool isHorizontalDominant = absDeltaX > absDeltaY;

            bool isLeft = deltaX < -StickDeadzone;
            bool isRight = deltaX > StickDeadzone;
            bool isUp = deltaY < -StickDeadzone;
            bool isDown = deltaY > StickDeadzone;

            var now = DateTime.Now;

            bool tryingNewDirection = false;

            if (isHorizontalDominant && (isLeft || isRight))
                tryingNewDirection = (isLeft && !_prevStickLeft) || (isRight && !_prevStickRight);
            else if (!isHorizontalDominant && (isUp || isDown))
                tryingNewDirection = (isUp && !_prevStickUp) || (isDown && !_prevStickDown);

            if (tryingNewDirection)
            {
                if (now - _lastStickDirectionChange < StickDirectionChangeDelay) return;

                _lastStickDirectionChange = now;
            }

            if (isHorizontalDominant && (isLeft || isRight))
            {
                ProcessStickDirection(isLeft, ref _prevStickLeft, _cachedDPadLeft, now);
                ProcessStickDirection(isRight, ref _prevStickRight, _cachedDPadRight, now);
                ProcessStickDirection(false, ref _prevStickUp, _cachedDPadUp, now);
                ProcessStickDirection(false, ref _prevStickDown, _cachedDPadDown, now);
            }
            else if (!isHorizontalDominant && (isUp || isDown))
            {
                ProcessStickDirection(isUp, ref _prevStickUp, _cachedDPadUp, now);
                ProcessStickDirection(isDown, ref _prevStickDown, _cachedDPadDown, now);
                ProcessStickDirection(false, ref _prevStickLeft, _cachedDPadLeft, now);
                ProcessStickDirection(false, ref _prevStickRight, _cachedDPadRight, now);
            }
            else
            {
                ProcessStickDirection(false, ref _prevStickUp, _cachedDPadUp, now);
                ProcessStickDirection(false, ref _prevStickDown, _cachedDPadDown, now);
                ProcessStickDirection(false, ref _prevStickLeft, _cachedDPadLeft, now);
                ProcessStickDirection(false, ref _prevStickRight, _cachedDPadRight, now);
            }
        }

        private void ProcessDirectionalInput(bool isActive, ref bool prevState, AKey key,
            ref DateTime pressStartTime, ref DateTime lastMoveTime, DateTime now)
        {
            if (isActive)
            {
                if (!prevState)
                {
                    SendKeyDown(key);
                    lastMoveTime = now;
                    pressStartTime = now;
                }
                else
                {
                    TimeSpan timeSincePress = now - pressStartTime;
                    TimeSpan timeSinceLastMove = now - lastMoveTime;

                    if (timeSincePress > InputInitialDelay && timeSinceLastMove > InputRepeatRate)
                    {
                        SendKeyDown(key);
                        lastMoveTime = now;
                    }
                }
            }
            else if (prevState)
            {
                SendKeyUp(key);
                pressStartTime = DateTime.MinValue;
            }

            prevState = isActive;
        }

        private void ProcessStickDirection(bool isActive, ref bool prevState, AKey key, DateTime now)
        {
            if (isActive)
            {
                if (!prevState)
                {
                    SendKeyDown(key);
                    _lastStickMoveTime = now;
                    _stickPressStartTime = now;
                }
                else
                {
                    TimeSpan timeSincePress = now - _stickPressStartTime;
                    TimeSpan timeSinceLastMove = now - _lastStickMoveTime;

                    if (timeSincePress > InputInitialDelay && timeSinceLastMove > InputRepeatRate)
                    {
                        SendKeyDown(key);
                        _lastStickMoveTime = now;
                    }
                }
            }
            else if (prevState)
            {
                SendKeyUp(key);
                _stickPressStartTime = DateTime.MinValue;
            }

            prevState = isActive;
        }

        private void HandleButtons(bool[] currentButtons)
        {
            MapButton(0, GamepadButton.ButtonA, _cachedButtonA, currentButtons);
            MapButton(1, GamepadButton.ButtonB, _cachedButtonB, currentButtons);
            MapButton(2, GamepadButton.ButtonX, _cachedButtonX, currentButtons);
            MapButton(3, GamepadButton.ButtonY, _cachedButtonY, currentButtons);
            MapButton(4, GamepadButton.LeftBumper, _cachedLeftBumper, currentButtons);
            MapButton(5, GamepadButton.RightBumper, _cachedRightBumper, currentButtons);
            MapButton(6, GamepadButton.Select, _cachedSelect, currentButtons);
            MapButton(7, GamepadButton.Start, _cachedStart, currentButtons);

            Array.Copy(currentButtons, _prevButtons, Math.Min(currentButtons.Length, _prevButtons.Length));
        }

        private void MapButton(int index, GamepadButton button, AKey key, bool[] currentButtons)
        {
            if (index >= currentButtons.Length) return;

            bool wasDown = _prevButtons[index];
            bool isDown = currentButtons[index];

            if (!wasDown && isDown)
            {
                SendButtonEvent(button, key);
                SendKeyUp(key);
            }
        }

        private static void SendKeyDown(AKey key)
        {
            var target = GetEventTarget();

            if (target == null) return;

            var args = new GamepadKeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                Source = target,
                IsFromGamepad = true
            };
            target.RaiseEvent(args);
        }

        private static void SendKeyUp(AKey key)
        {
            var target = GetEventTarget();

            if (target == null) return;

            var args = new GamepadKeyEventArgs
            {
                RoutedEvent = InputElement.KeyUpEvent,
                Key = key,
                Source = target,
                IsFromGamepad = true
            };
            target.RaiseEvent(args);
        }

        private static void SendButtonEvent(GamepadButton button, AKey key)
        {
            var target = GetEventTarget();

            if (target == null) return;

            var args = new GamepadKeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                Source = target,
                IsFromGamepad = true,
                OriginalButton = button
            };
            target.RaiseEvent(args);
        }

        private static InputElement? GetEventTarget()
        {
            var app = Avalonia.Application.Current;

            if (app?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var focused = desktop.MainWindow?.FocusManager?.GetFocusedElement() as InputElement;

                return focused ?? desktop.MainWindow as InputElement;
            }

            return null;
        }

        public void Dispose()
        {
            InputManager.KeyMappingsChanged -= OnKeyMappingsChanged;

            _timer?.Stop();

            foreach (var joystick in _gamepads)
            {
                try
                {
                    joystick.Unacquire();
                    joystick.Dispose();
                }
                catch { }
            }
            _directInput.Dispose();
        }
    }
}