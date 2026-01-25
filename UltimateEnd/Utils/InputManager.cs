using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Utils
{
    public static class InputManager
    {
        private static readonly Dictionary<GamepadButton, Key> _keyMappings = [];

        public static event EventHandler? KeyMappingsChanged;

        public static void LoadKeyBindings()
        {
            var settings = SettingsService.LoadSettings();

            _keyMappings.Clear();

            if (settings.KeyBindings != null && settings.KeyBindings.Count > 0)
            {
                _keyMappings[GamepadButton.DPadUp] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadUp", "Up"));
                _keyMappings[GamepadButton.DPadDown] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadDown", "Down"));
                _keyMappings[GamepadButton.DPadLeft] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadLeft", "Left"));
                _keyMappings[GamepadButton.DPadRight] = ParseKey(settings.KeyBindings.GetValueOrDefault("DPadRight", "Right"));
                _keyMappings[GamepadButton.ButtonA] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonA", "Return"));
                _keyMappings[GamepadButton.ButtonB] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonB", "Escape"));
                _keyMappings[GamepadButton.ButtonX] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonX", "X"));
                _keyMappings[GamepadButton.ButtonY] = ParseKey(settings.KeyBindings.GetValueOrDefault("ButtonY", "F"));
                _keyMappings[GamepadButton.LeftBumper] = ParseKey(settings.KeyBindings.GetValueOrDefault("LeftBumper", "PageUp"));
                _keyMappings[GamepadButton.RightBumper] = ParseKey(settings.KeyBindings.GetValueOrDefault("RightBumper", "PageDown"));
                _keyMappings[GamepadButton.LeftTrigger] = ParseKey(settings.KeyBindings.GetValueOrDefault("LeftTrigger", "LeftCtrl"));
                _keyMappings[GamepadButton.RightTrigger] = ParseKey(settings.KeyBindings.GetValueOrDefault("RightTrigger", "LeftAlt"));
                _keyMappings[GamepadButton.Start] = ParseKey(settings.KeyBindings.GetValueOrDefault("Start", "Space"));
                _keyMappings[GamepadButton.Select] = ParseKey(settings.KeyBindings.GetValueOrDefault("Select", "F1"));
            }
            else
                SetDefaultKeyBindings(_keyMappings);

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
            mappings[GamepadButton.Start] = Key.Space;
            mappings[GamepadButton.Select] = Key.F1;
        }

        private static Key ParseKey(string keyString)
        {
            if (Enum.TryParse<Key>(keyString, out var key)) return key;
            return Key.None;
        }

        public static bool IsGamepadButtonPressed(KeyEventArgs e, GamepadButton button)
        {
            if (e is not GamepadKeyEventArgs gpe || !gpe.IsFromGamepad) return false;

            return gpe.OriginalButton == button;
        }

        public static bool IsKeyboardKeyPressed(KeyEventArgs e, GamepadButton button)
        {
            if (e is GamepadKeyEventArgs gpe && gpe.IsFromGamepad) return false;

            if (IsTextInputFocused())
            {
                bool isAllowedInTextBox = e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Up || e.Key == Key.Down;
                
                if (!isAllowedInTextBox) return false;
            }

            if (!_keyMappings.TryGetValue(button, out var mappedKey)) return false;

            return mappedKey == e.Key;
        }

        public static bool IsButtonPressed(KeyEventArgs e, GamepadButton button) => IsGamepadButtonPressed(e, button) || IsKeyboardKeyPressed(e, button);


        public static bool IsAnyButtonPressed(KeyEventArgs e, params GamepadButton[] buttons)
        {
            foreach (var button in buttons)
            {
                if (IsButtonPressed(e, button)) return true;
            }

            return false;
        }

        public static Key GetMappedKey(GamepadButton button)
        {
            var result = _keyMappings.TryGetValue(button, out var key) ? key : Key.None;

            System.Diagnostics.Debug.WriteLine($"[InputManager] GetMappedKey({button}): {result}");

            return result;
        }

        public static GamepadButton? GetMappedButton(Key key)
        {
            foreach (var kvp in _keyMappings)
            {
                if (kvp.Value == key) return kvp.Key;
            }

            return null;
        }

        private static bool IsTextInputFocused()
        {
            var app = Avalonia.Application.Current;

            if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var focused = desktop.MainWindow?.FocusManager?.GetFocusedElement();

                return focused is TextBox || focused is ComboBox || focused is Slider;
            }
            else if (app?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime single)
            {
                var mainView = single.MainView as Control;
                var topLevel = mainView?.GetVisualRoot() as TopLevel;
                var focused = topLevel?.FocusManager?.GetFocusedElement();

                return focused is TextBox || focused is ComboBox || focused is Slider;
            }

            return false;
        }
    }
}