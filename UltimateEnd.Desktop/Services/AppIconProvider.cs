using System;
using System.IO;
using System.Runtime.InteropServices;
using UltimateEnd.Services;
using System.Drawing.Imaging;

namespace UltimateEnd.Desktop.Services
{
    public class AppIconProvider : IAppIconProvider
    {
        private const int SHGFI_ICON = 0x100;
        private const int SHGFI_LARGEICON = 0x0;
        private const int SHGFI_SMALLICON = 0x1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public Avalonia.Media.Imaging.Bitmap GetAppIcon(string launchCommand)
        {
            if (string.IsNullOrWhiteSpace(launchCommand))
                return null!;

            try
            {
                string executablePath = ExtractExecutablePath(launchCommand);

                if (string.IsNullOrEmpty(executablePath))
                    return null!;

                if (!Path.IsPathRooted(executablePath))
                    executablePath = Path.Combine(AppContext.BaseDirectory, executablePath);

                if (!File.Exists(executablePath))
                    return null!;

                return ExtractIconFromFile(executablePath)!;
            }
            catch
            {
                return null!;
            }
        }

        private string ExtractExecutablePath(string launchCommand)
        {
            launchCommand = launchCommand.Trim();

            if (launchCommand.StartsWith("\""))
            {
                var endQuoteIndex = launchCommand.IndexOf("\"", 1);

                if (endQuoteIndex > 0)
                    return launchCommand.Substring(1, endQuoteIndex - 1);
            }

            var firstSpaceIndex = launchCommand.IndexOf(' ');

            if (firstSpaceIndex > 0)
                return launchCommand.Substring(0, firstSpaceIndex);

            return launchCommand;
        }

        private Avalonia.Media.Imaging.Bitmap? ExtractIconFromFile(string filePath)
        {
            var shinfo = new SHFILEINFO();
            var result = SHGetFileInfo(filePath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);

            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
                return null;

            try
            {
                using var icon = System.Drawing.Icon.FromHandle(shinfo.hIcon);
                using var bitmap = icon.ToBitmap();

                return ConvertToAvaloniaBitmap(bitmap);
            }
            finally
            {
                DestroyIcon(shinfo.hIcon);
            }
        }
        private Avalonia.Media.Imaging.Bitmap ConvertToAvaloniaBitmap(System.Drawing.Bitmap drawingBitmap)
        {
            using var memoryStream = new MemoryStream();

            drawingBitmap.Save(memoryStream, ImageFormat.Png);
            memoryStream.Position = 0;

            return new Avalonia.Media.Imaging.Bitmap(memoryStream);
        }
    }
}