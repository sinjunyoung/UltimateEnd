using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using UltimateEnd.Enums;
using UltimateEnd.Utils;

namespace UltimateEnd.Behaviors
{
    public static class InputMappingBehavior
    {
        public static readonly AttachedProperty<bool> EnableProperty = AvaloniaProperty.RegisterAttached<Control, bool>("Enable", typeof(InputMappingBehavior));

        static InputMappingBehavior()
        {
            EnableProperty.Changed.AddClassHandler<Control>(OnEnableChanged);
        }

        public static void SetEnable(Control element, bool value) => element.SetValue(EnableProperty, value);

        public static bool GetEnable(Control element) => element.GetValue(EnableProperty);

        private static void OnEnableChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue!)
                control.KeyDown += OnKeyDown;
            else
                control.KeyDown -= OnKeyDown;
        }

        private static void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right ||
                e.Key == Key.Enter || e.Key == Key.Escape)
                return;

            Key translatedKey = Key.None;

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadUp))
                translatedKey = Key.Up;
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadDown))
                translatedKey = Key.Down;
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadLeft))
                translatedKey = Key.Left;
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadRight))
                translatedKey = Key.Right;
            else if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
                translatedKey = Key.Enter;
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
                translatedKey = Key.Escape;
            else
                return;

            if (sender is Control control)
            {
                e.Handled = true;

                control.KeyDown -= OnKeyDown;

                var newEvent = new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = translatedKey,
                    Source = control
                };
                control.RaiseEvent(newEvent);

                control.KeyDown += OnKeyDown;
            }
        }
    }
}