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
        [Guid("37c994e7-432b-4834-a2f7-dce1f13b834b")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ITipInvocation
        {
            void Toggle(IntPtr hwnd);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        public static readonly AttachedProperty<bool> EnableProperty = AvaloniaProperty.RegisterAttached<TouchKeyboardBehavior, Control, bool>("Enable", defaultValue: false);

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
            try
            {
                Guid CLSID_UIHostNoLaunch = new("4ce576fa-83dc-4f88-951c-9d0782b4e376");
                Type? comType = Type.GetTypeFromCLSID(CLSID_UIHostNoLaunch);

                if (comType != null)
                {
                    object? instance = Activator.CreateInstance(comType);

                    if (instance is ITipInvocation tip)
                    {
                        tip.Toggle(IntPtr.Zero);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"COM 호출 에러: {ex.Message}");
            }

            try
            {
                Process.Start(new ProcessStartInfo(@"C:\Common Files\Microsoft Shared\ink\TabTip.exe")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        public static void HideKeyboard()
        {
            try
            {
                IntPtr hwnd = FindWindow("IPTip_Main_Window", null);

                if (hwnd != IntPtr.Zero)
                    ShowKeyboard();
            }
            catch { }
        }
    }
}