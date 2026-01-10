using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.Runtime.InteropServices;

namespace UltimateEnd.Desktop.Controls
{
    public class VideoHost : NativeControlHost
    {
        private nint _hwnd;

        #region Win32

        private const int BLACK_BRUSH = 4;

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(nint hWnd, int nCmdShow);
        [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
        private static extern nint SetClassLongPtr(nint hWnd, int nIndex, nint dwNewLong);
        [DllImport("gdi32.dll")]
        private static extern nint GetStockObject(int fnObject);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern nint CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(nint hWnd);
        [DllImport("user32.dll")]
        private static extern nint SetParent(nint hWndChild, nint hWndNewParent);
        [DllImport("gdi32.dll")]
        private static extern nint CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);
        [DllImport("user32.dll")]
        private static extern int SetWindowRgn(nint hWnd, nint hRgn, bool bRedraw);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(nint hWnd, nint lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(nint hWnd);

        [DllImport("user32.dll")]
        private static extern nint DefWindowProc(nint hWnd, uint uMsg, nint wParam, nint lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern nint GetModuleHandle(string lpModuleName);

        private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public nint lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string lpszClassName;
            public nint hIconSm;
        }

        private static bool _classRegistered = false;
        private const string CUSTOM_CLASS_NAME = "BlackVideoWindowClass";
        private static WndProcDelegate _staticWndProc;

        private static nint CustomWndProc(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }

        #endregion

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle? parent)
        {
            var parentHwnd = parent?.Handle ?? nint.Zero;

            if (!_classRegistered)
            {
                _staticWndProc = CustomWndProc;

                WNDCLASSEX wc = new()
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_staticWndProc),
                    hInstance = GetModuleHandle(null!),
                    hbrBackground = GetStockObject(BLACK_BRUSH),
                    lpszClassName = CUSTOM_CLASS_NAME
                };

                if (RegisterClassEx(ref wc) != 0) _classRegistered = true;
            }

            _hwnd = CreateWindowEx(0, CUSTOM_CLASS_NAME, string.Empty, 0x40000000 | 0x10000000 | 0x02000000 | 0x04000000, 0, 0, (int)Math.Max(1, Bounds.Width), (int)Math.Max(1, Bounds.Height), parentHwnd, nint.Zero, GetModuleHandle(null!), nint.Zero);

            return new PlatformHandle(_hwnd, "HWND");
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            if (_hwnd != nint.Zero)
            {
                SetParent(_hwnd, nint.Zero);
                DestroyWindow(_hwnd);
                _hwnd = nint.Zero;
            }
            base.DestroyNativeControlCore(control);
        }

        public void ApplyRounding(int width, int height, int radius)
        {
            if (_hwnd == nint.Zero || width <= 0 || height <= 0) return;
            nint hRgn = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
            SetWindowRgn(_hwnd, hRgn, true);
        }

        public nint GetHandle() => _hwnd;        
    }
}