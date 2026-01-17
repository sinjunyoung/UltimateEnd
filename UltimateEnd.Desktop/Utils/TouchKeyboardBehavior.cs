using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UltimateEnd.Desktop.Utils
{
    public partial class TouchKeyboardBehavior
    {

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern int PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static readonly AttachedProperty<bool> EnableProperty =
            AvaloniaProperty.RegisterAttached<TouchKeyboardBehavior, Control, bool>("Enable", defaultValue: false);

        static TouchKeyboardBehavior() => EnableProperty.Changed.Subscribe(OnEnableChanged);

        private static void OnEnableChanged(AvaloniaPropertyChangedEventArgs<bool> args)
        {
            if (args.Sender is TextBox textBox)
            {
                textBox.AddHandler(InputElement.PointerPressedEvent, (s, e) => {
                    if (e.Pointer.Type == PointerType.Touch)
                        ShowKeyboard();
                }, RoutingStrategies.Tunnel, true);

                textBox.LostFocus += (s, e) => HideKeyboard();
            }
        }

        public static void ShowKeyboard()
        {
            IntPtr hwnd = FindWindow("IPTip_Main_Window", null);
            if (hwnd != IntPtr.Zero) return;

            try
            {
                Process.Start(new ProcessStartInfo("TabTip.exe") { UseShellExecute = true });
                
            }
            catch { }
        }

        public static void HideKeyboard()
        {
            IntPtr hwnd = FindWindow("IPTip_Main_Window", null);

            if (hwnd != IntPtr.Zero) PostMessage(hwnd, 0x0112, (IntPtr)0xF060, IntPtr.Zero);
        }
    }
}