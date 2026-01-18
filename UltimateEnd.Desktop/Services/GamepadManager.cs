using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using UltimateEnd.Desktop.Models;
using UltimateEnd.Enums;
using UltimateEnd.Services;
using UltimateEnd.Utils;

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

        private ButtonMapping _buttonMapping;

        public GamepadManager()
        {
            _buttonMapping = LoadButtonMapping();
            InitializeDevices();

            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, (s, e) => PollInput());
            _timer.Start();
        }

        private static ButtonMapping LoadButtonMapping()
        {
            var settings = SettingsService.LoadSettings();

            return settings.ControllerType switch
            {
                "Xbox" => ButtonMapping.XboxStyle(),
                "PlayStation" => ButtonMapping.PlayStationStyle(),
                "Switch" => ButtonMapping.SwitchStyle(),
                _ => ButtonMapping.XboxStyle()
            };
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

                    if (_gamepads.Count == 1)
                    {
                        var settings = SettingsService.LoadSettings();
                        if (settings.ControllerType == "Auto")
                        {
                            _buttonMapping = DetectControllerType(joystick);
                            System.Diagnostics.Debug.WriteLine($"컨트롤러 자동 감지: {joystick.Information.ProductName} -> 매핑 적용됨");
                        }
                    }
                }
                catch { }
            }
        }

        private static ButtonMapping DetectControllerType(Joystick joystick)
        {
            var name = joystick.Information.ProductName.ToLower();

            if (name.Contains("dualsense") || name.Contains("dualshock") ||
                name.Contains("wireless controller") || name.Contains("ps4") || name.Contains("ps5"))
            {
                System.Diagnostics.Debug.WriteLine("PlayStation 컨트롤러 감지");
                return ButtonMapping.PlayStationStyle();
            }

            if (name.Contains("pro controller") || name.Contains("switch"))
            {
                System.Diagnostics.Debug.WriteLine("Switch 컨트롤러 감지");
                return ButtonMapping.SwitchStyle();
            }

            System.Diagnostics.Debug.WriteLine("Xbox 컨트롤러로 감지");
            return ButtonMapping.XboxStyle();
        }

        private void PollInput()
        {
            if (_gamepads.Count == 0 || _isProcessing) return;

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

            ProcessDirectionalInput(isUp, ref _prevDpadUp, GamepadButton.DPadUp, ref _dpadPressStartTime, ref _lastDpadMoveTime, now);
            ProcessDirectionalInput(isDown, ref _prevDpadDown, GamepadButton.DPadDown, ref _dpadPressStartTime, ref _lastDpadMoveTime, now);
            ProcessDirectionalInput(isLeft, ref _prevDpadLeft, GamepadButton.DPadLeft, ref _dpadPressStartTime, ref _lastDpadMoveTime, now);
            ProcessDirectionalInput(isRight, ref _prevDpadRight, GamepadButton.DPadRight, ref _dpadPressStartTime, ref _lastDpadMoveTime, now);
        }

        private void ProcessDirectionalInput(bool isActive, ref bool prevState, GamepadButton button,
            ref DateTime pressStartTime, ref DateTime lastMoveTime, DateTime now)
        {
            if (isActive)
            {
                if (!prevState)
                {
                    SendKeyDown(button);
                    lastMoveTime = now;
                    pressStartTime = now;
                }
                else
                {
                    TimeSpan timeSincePress = now - pressStartTime;
                    TimeSpan timeSinceLastMove = now - lastMoveTime;
                    if (timeSincePress > InputInitialDelay && timeSinceLastMove > InputRepeatRate)
                    {
                        SendKeyDown(button);
                        lastMoveTime = now;
                    }
                }
            }
            else if (prevState)
            {
                SendKeyUp(button);
                pressStartTime = DateTime.MinValue;
            }
            prevState = isActive;
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
                ProcessStickDirection(isLeft, ref _prevStickLeft, GamepadButton.DPadLeft, now);
                ProcessStickDirection(isRight, ref _prevStickRight, GamepadButton.DPadRight, now);
                ProcessStickDirection(false, ref _prevStickUp, GamepadButton.DPadUp, now);
                ProcessStickDirection(false, ref _prevStickDown, GamepadButton.DPadDown, now);
            }
            else if (!isHorizontalDominant && (isUp || isDown))
            {
                ProcessStickDirection(isUp, ref _prevStickUp, GamepadButton.DPadUp, now);
                ProcessStickDirection(isDown, ref _prevStickDown, GamepadButton.DPadDown, now);
                ProcessStickDirection(false, ref _prevStickLeft, GamepadButton.DPadLeft, now);
                ProcessStickDirection(false, ref _prevStickRight, GamepadButton.DPadRight, now);
            }
            else
            {
                ProcessStickDirection(false, ref _prevStickUp, GamepadButton.DPadUp, now);
                ProcessStickDirection(false, ref _prevStickDown, GamepadButton.DPadDown, now);
                ProcessStickDirection(false, ref _prevStickLeft, GamepadButton.DPadLeft, now);
                ProcessStickDirection(false, ref _prevStickRight, GamepadButton.DPadRight, now);
            }
        }

        private void ProcessStickDirection(bool isActive, ref bool prevState, GamepadButton button, DateTime now)
        {
            if (isActive)
            {
                if (!prevState)
                {
                    SendKeyDown(button);
                    _lastStickMoveTime = now;
                    _stickPressStartTime = now;
                }
                else
                {
                    TimeSpan timeSincePress = now - _stickPressStartTime;
                    TimeSpan timeSinceLastMove = now - _lastStickMoveTime;
                    if (timeSincePress > InputInitialDelay && timeSinceLastMove > InputRepeatRate)
                    {
                        SendKeyDown(button);
                        _lastStickMoveTime = now;
                    }
                }
            }
            else if (prevState)
            {
                SendKeyUp(button);
                _stickPressStartTime = DateTime.MinValue;
            }
            prevState = isActive;
        }

        private void HandleButtons(bool[] currentButtons)
        {
            MapButton(_buttonMapping.A, GamepadButton.ButtonA, currentButtons);
            MapButton(_buttonMapping.B, GamepadButton.ButtonB, currentButtons);
            MapButton(_buttonMapping.X, GamepadButton.ButtonX, currentButtons);
            MapButton(_buttonMapping.Y, GamepadButton.ButtonY, currentButtons);
            MapButton(_buttonMapping.LB, GamepadButton.LeftBumper, currentButtons);
            MapButton(_buttonMapping.RB, GamepadButton.RightBumper, currentButtons);
            MapButton(_buttonMapping.Select, GamepadButton.Select, currentButtons);
            MapButton(_buttonMapping.Start, GamepadButton.Start, currentButtons);

            Array.Copy(currentButtons, _prevButtons, Math.Min(currentButtons.Length, _prevButtons.Length));
        }

        private void MapButton(int index, GamepadButton button, bool[] currentButtons)
        {
            if (index >= currentButtons.Length) return;
            bool wasDown = _prevButtons[index];
            bool isDown = currentButtons[index];

            if (!wasDown && isDown)
            {
                SendButtonEvent(button);
                SendKeyUp(button);
            }
        }

        private static void SendKeyDown(GamepadButton button)
        {
            var target = GetEventTarget();
            if (target == null) return;

            var key = InputManager.GetMappedKey(button);

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

        private static void SendKeyUp(GamepadButton button)
        {
            var target = GetEventTarget();
            if (target == null) return;

            var key = InputManager.GetMappedKey(button);

            var args = new GamepadKeyEventArgs
            {
                RoutedEvent = InputElement.KeyUpEvent,
                Key = key,
                Source = target,
                IsFromGamepad = true,
                OriginalButton = button
            };
            target.RaiseEvent(args);
        }

        private static void SendButtonEvent(GamepadButton button)
        {
            var target = GetEventTarget();
            if (target == null) return;

            var key = InputManager.GetMappedKey(button);

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