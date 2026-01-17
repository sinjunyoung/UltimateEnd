using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using System;

namespace UltimateEnd.Desktop.Utils
{
    public class TouchKeyboardBehavior
    {
        public static readonly AttachedProperty<bool> EnableProperty = AvaloniaProperty.RegisterAttached<TouchKeyboardBehavior, Control, bool>("Enable", defaultValue: false);

        static TouchKeyboardBehavior() => EnableProperty.Changed.Subscribe(OnEnableChanged);

        public static void SetEnable(Control element, bool value) => element.SetValue(EnableProperty, value);

        public static bool GetEnable(Control element)
        {
            return element.GetValue(EnableProperty);
        }

        private static void OnEnableChanged(AvaloniaPropertyChangedEventArgs<bool> args)
        {
            if (args.Sender is TextBox textBox)
            {
                textBox.GotFocus -= OnTextBoxGotFocus;

                if (args.NewValue.Value)
                    textBox.GotFocus += OnTextBoxGotFocus;
            }
        }

        private static void OnTextBoxGotFocus(object? sender, GotFocusEventArgs e) => TouchKeyboardHelper.ShowKeyboardIfNeeded();
    }
}