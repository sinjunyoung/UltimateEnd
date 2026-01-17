using System;
using System.Runtime.InteropServices;

namespace UltimateEnd.Desktop.Utils
{
    public static class TouchKeyboardHelper
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_DIGITIZER = 94;
        private const int NID_INTEGRATED_TOUCH = 0x01;
        private const int NID_EXTERNAL_TOUCH = 0x02;

        public static bool IsTouchEnabled()
        {
            int digitizer = GetSystemMetrics(SM_DIGITIZER);
            return (digitizer & (NID_INTEGRATED_TOUCH | NID_EXTERNAL_TOUCH)) != 0;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_SYSCOMMAND = 0x0112;
        private const uint SC_TABTIP = 0xF6B0;

        public static void ShowKeyboardIfNeeded()
        {
            if (IsTouchEnabled())
            {
                try
                {
                    var hwnd = GetDesktopWindow();
                    var result = PostMessage(hwnd, WM_SYSCOMMAND, new IntPtr(SC_TABTIP), IntPtr.Zero);                    
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"에러: {ex.Message}");
                }
            }
        }
    }
}