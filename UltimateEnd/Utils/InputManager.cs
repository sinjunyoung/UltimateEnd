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
        private static readonly Lazy<Dictionary<GamepadButton, Key>> _keyMappings =
            new(() =>
            {
                var mappings = new Dictionary<GamepadButton, Key>();
                SetDefaultKeyBindings(mappings);

                return mappings;
            });

        public static event EventHandler? KeyMappingsChanged;

        public static void LoadKeyBindings()
        {
            var settings = SettingsService.LoadSettings();

            if (settings.KeyBindings != null && settings.KeyBindings.Count > 0)
            {
                _keyMappings.Value[GamepadButton.DPadUp] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadUp", "Up"));
                _keyMappings.Value[GamepadButton.DPadDown] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadDown", "Down"));
                _keyMappings.Value[GamepadButton.DPadLeft] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadLeft", "Left"));
                _keyMappings.Value[GamepadButton.DPadRight] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadRight", "Right"));
                _keyMappings.Value[GamepadButton.ButtonA] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonA", "Return"));
                _keyMappings.Value[GamepadButton.ButtonB] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonB", "Escape"));
                _keyMappings.Value[GamepadButton.ButtonX] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonX", "X"));
                _keyMappings.Value[GamepadButton.ButtonY] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonY", "F"));
                _keyMappings.Value[GamepadButton.LeftBumper] = ParseKey(settings.KeyBindings.GetValueOrDefault("LeftBumper", "PageUp"));
                _keyMappings.Value[GamepadButton.RightBumper] = ParseKey(settings.KeyBindings.GetValueOrDefault("RightBumper", "PageDown"));
                _keyMappings.Value[GamepadButton.LeftTrigger] = ParseKey(settings.KeyBindings.GetValueOrDefault("LeftTrigger", "LeftCtrl"));
                _keyMappings.Value[GamepadButton.RightTrigger] = ParseKey(settings.KeyBindings.GetValueOrDefault("RightTrigger", "LeftAlt"));
                _keyMappings.Value[GamepadButton.Start] = ParseKey(settings.KeyBindings.GetValueOrDefault("Start", "Return"));
                _keyMappings.Value[GamepadButton.Select] = ParseKey(settings.KeyBindings.GetValueOrDefault("Select", "Escape"));
            }
            else
                SetDefaultKeyBindings(_keyMappings.Value);

            KeyMappingsChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void SetDefaultKeyBindings(Dictionary<GamepadButton, Key> mappings)
        {
            mappings[GamepadButton.DPadUp] = Key.Up;
            mappings[GamepadButton.DPadDown] = Key.Down;
            mappings[GamepadButton.DPadLeft] = Key.Left;
            mappings[GamepadButton.DPadRight] = Key.Right;
            mappings[GamepadButton.ButtonA] = Key.Return;
            mappings[GamepadButton.ButtonB] = Key.Escape;
            mappings[GamepadButton.ButtonX] = Key.X;
            mappings[GamepadButton.ButtonY] = Key.F;
            mappings[GamepadButton.LeftBumper] = Key.PageUp;
            mappings[GamepadButton.RightBumper] = Key.PageDown;
            mappings[GamepadButton.LeftTrigger] = Key.LeftCtrl;
            mappings[GamepadButton.RightTrigger] = Key.LeftAlt;
            mappings[GamepadButton.Start] = Key.Return;
            mappings[GamepadButton.Select] = Key.Escape;
        }

        private static Key ParseKey(string keyString)
        {
            if (Enum.TryParse<Key>(keyString, out var key)) return key;

            return Key.None;
        }

        public static bool IsButtonPressed(Key key, GamepadButton button)
        {
            if (!_keyMappings.Value.TryGetValue(button, out var mappedKey)) return false;

            if (mappedKey != key) return false;

            if (IsTextInputFocused())
            {
                bool isAllowedInTextBox = key == Key.Escape || key == Key.Enter;

                if (!isAllowedInTextBox) return false;
            }

            return true;
        }

        public static bool IsAnyButtonPressed(Key key, params GamepadButton[] buttons)
        {
            foreach (var button in buttons)
            {
                if (IsButtonPressed(key, button)) return true;
            }

            return false;
        }

        public static GamepadButton? GetMappedButton(Key key)
        {
            foreach (var kvp in _keyMappings.Value)
            {
                if (kvp.Value == key) return kvp.Key;
            }

            return null;
        }

        public static Key GetMappedKey(GamepadButton button) => _keyMappings.Value.TryGetValue(button, out var key) ? key : Key.None;

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