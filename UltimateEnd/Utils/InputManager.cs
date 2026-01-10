using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using UltimateEnd.Enums;
using UltimateEnd.Services;

namespace UltimateEnd.Utils
{
    public static class InputManager
    {
        private static readonly Dictionary<GamepadButton, Key> _keyMappings = [];

        static InputManager() => LoadKeyBindings();

        public static void LoadKeyBindings()
        {
            var settings = SettingsService.LoadSettings();

            if (settings.KeyBindings != null && settings.KeyBindings.Count > 0)
            {
                _keyMappings[GamepadButton.DPadUp] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadUp", "Up"));
                _keyMappings[GamepadButton.DPadDown] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadDown", "Down"));
                _keyMappings[GamepadButton.DPadLeft] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadLeft", "Left"));
                _keyMappings[GamepadButton.DPadRight] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadRight", "Right"));
                _keyMappings[GamepadButton.ButtonA] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonA", "Enter"));
                _keyMappings[GamepadButton.ButtonB] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonB", "Escape"));
                _keyMappings[GamepadButton.ButtonX] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonX", "X"));
                _keyMappings[GamepadButton.ButtonY] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonY", "F"));
                _keyMappings[GamepadButton.LeftBumper] = ParseKey(settings.KeyBindings.GetValueOrDefault("LeftBumper", "PageUp"));
                _keyMappings[GamepadButton.RightBumper] = ParseKey(settings.KeyBindings.GetValueOrDefault("RightBumper", "PageDown"));
                _keyMappings[GamepadButton.LeftTrigger] = ParseKey(settings.KeyBindings.GetValueOrDefault("LeftTrigger", "LeftCtrl"));
                _keyMappings[GamepadButton.RightTrigger] = ParseKey(settings.KeyBindings.GetValueOrDefault("RightTrigger", "LeftAlt"));
                _keyMappings[GamepadButton.Start] = ParseKey(settings.KeyBindings.GetValueOrDefault("Start", "Enter"));
                _keyMappings[GamepadButton.Select] = ParseKey(settings.KeyBindings.GetValueOrDefault("Select", "Escape"));
            }
            else
                SetDefaultKeyBindings();
        }

        private static void SetDefaultKeyBindings()
        {
            _keyMappings[GamepadButton.DPadUp] = Key.Up;
            _keyMappings[GamepadButton.DPadDown] = Key.Down;
            _keyMappings[GamepadButton.DPadLeft] = Key.Left;
            _keyMappings[GamepadButton.DPadRight] = Key.Right;
            _keyMappings[GamepadButton.ButtonA] = Key.Enter;
            _keyMappings[GamepadButton.ButtonB] = Key.Escape;
            _keyMappings[GamepadButton.ButtonX] = Key.X;
            _keyMappings[GamepadButton.ButtonY] = Key.F;
            _keyMappings[GamepadButton.LeftBumper] = Key.PageUp;
            _keyMappings[GamepadButton.RightBumper] = Key.PageDown;
            _keyMappings[GamepadButton.LeftTrigger] = Key.LeftCtrl;
            _keyMappings[GamepadButton.RightTrigger] = Key.LeftAlt;
            _keyMappings[GamepadButton.Start] = Key.Enter;
            _keyMappings[GamepadButton.Select] = Key.Escape;
        }

        private static Key ParseKey(string keyString)
        {
            if (Enum.TryParse<Key>(keyString, out var key))
                return key;

            return Key.None;
        }

        public static bool IsButtonPressed(Key key, GamepadButton button)
        {
            if (!_keyMappings.TryGetValue(button, out var mappedKey))
                return false;

            if (mappedKey != key)
                return false;

            if (IsTextInputFocused())
            {
                bool isAllowedInTextBox = key == Key.Escape || key == Key.Enter;
                if (!isAllowedInTextBox)
                    return false;
            }

            return true;
        }

        public static bool IsAnyButtonPressed(Key key, params GamepadButton[] buttons)
        {
            foreach (var button in buttons)
            {
                if (IsButtonPressed(key, button))
                    return true;
            }

            return false;
        }

        public static GamepadButton? GetMappedButton(Key key)
        {
            foreach (var kvp in _keyMappings)
            {
                if (kvp.Value == key)
                    return kvp.Key;
            }

            return null;
        }

        public static Key GetMappedKey(GamepadButton button) => _keyMappings.TryGetValue(button, out var key) ? key : Key.None;

        private static bool IsTextInputFocused()
        {
            var app = Avalonia.Application.Current;

            if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var focused = desktop.MainWindow?.FocusManager?.GetFocusedElement();
                return focused is TextBox || focused is ComboBox;
            }
            else if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime single)
            {
                var mainView = single.MainView as Control;
                var topLevel = mainView?.GetVisualRoot() as TopLevel;
                var focused = topLevel?.FocusManager?.GetFocusedElement();
                return focused is TextBox || focused is ComboBox;
            }

            return false;
        }
    }
}