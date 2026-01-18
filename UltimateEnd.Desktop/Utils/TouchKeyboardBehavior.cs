using Avalonia;
using Avalonia.Controls;
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

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOW = 5;

        public static readonly AttachedProperty<bool> EnableProperty = AvaloniaProperty.RegisterAttached<TouchKeyboardBehavior, Control, bool>("Enable", defaultValue: false);

        static TouchKeyboardBehavior() => EnableProperty.Changed.Subscribe(OnEnableChanged);

        private static void OnEnableChanged(AvaloniaPropertyChangedEventArgs<bool> args)
        {
            if (args.Sender is TextBox textBox)
            {
                textBox.GotFocus += (s, e) => ShowKeyboard();
                textBox.LostFocus += (s, e) => HideKeyboard();
            }
        }

        public static void ShowKeyboard()
        {
            IntPtr hwnd = FindWindow("IPTip_Main_Window", null);

            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, SW_SHOW);
                return;
            }

            string tabTipPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), @"microsoft shared\ink\TabTip.exe");

            if (!System.IO.File.Exists(tabTipPath))
                tabTipPath = @"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe";

            if (!System.IO.File.Exists(tabTipPath))
                tabTipPath = @"C:\Program Files (x86)\Common Files\microsoft shared\ink\TabTip.exe";

            Process.Start(new ProcessStartInfo
            {
                FileName = tabTipPath,
                WorkingDirectory = System.IO.Path.GetDirectoryName(tabTipPath),
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            });
        }

        public static void HideKeyboard()
        {
            IntPtr hwnd = FindWindow("IPTip_Main_Window", null);

            if (hwnd != IntPtr.Zero) _ = PostMessage(hwnd, 0x0112, 0xF060, IntPtr.Zero);
        }
    }
}