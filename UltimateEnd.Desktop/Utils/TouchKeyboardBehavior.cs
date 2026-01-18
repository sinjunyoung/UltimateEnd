using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace UltimateEnd.Desktop.Utils
{
    public partial class TouchKeyboardBehavior
    {

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern int PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static readonly AttachedProperty<bool> EnableProperty = AvaloniaProperty.RegisterAttached<TouchKeyboardBehavior, Control, bool>("Enable", defaultValue: false);

        static TouchKeyboardBehavior() => EnableProperty.Changed.Subscribe(OnEnableChanged);

        private static void OnEnableChanged(AvaloniaPropertyChangedEventArgs<bool> args)
        {
            if (args.Sender is TextBox textBox)
            {
                textBox.AddHandler(InputElement.PointerPressedEvent, (s, e) => {
                        ShowKeyboard();
                }, RoutingStrategies.Tunnel, true);

                textBox.LostFocus += (s, e) => HideKeyboard();
            }
        }

        public static void ShowKeyboard()
        {
            if (IsPhysicalKeyboardAttached()) return;

            IntPtr hwnd = FindWindow("IPTip_Main_Window", null);
            if (hwnd != IntPtr.Zero) return;

            try
            {
                string commonFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
                string tabTipPath = System.IO.Path.Combine(commonFiles, @"microsoft shared\ink\TabTip.exe");

                if (!System.IO.File.Exists(tabTipPath))
                    tabTipPath = tabTipPath.Replace("Program Files (x86)", "Program Files");

                Process.Start(new ProcessStartInfo
                {
                    FileName = tabTipPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"터치 키보드 실행 실패: {ex.Message}");
            }
        }

        public static void HideKeyboard()
        {
            if (IsPhysicalKeyboardAttached()) return;

            IntPtr hwnd = FindWindow("IPTip_Main_Window", null);

            if (hwnd != IntPtr.Zero) PostMessage(hwnd, 0x0112, (IntPtr)0xF060, IntPtr.Zero);
        }

        private static bool IsPhysicalKeyboardAttached()
        {
            try
            {
                var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_Keyboard WHERE DeviceID LIKE '%USB%'"
                );

                int count = searcher.Get().Count;
                return count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}