using System;
using System.Runtime.InteropServices;

namespace UltimateEnd.Desktop.Utils
{
    public class ScreenSaverBlocker
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [FlagsAttribute]
        public enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS = 0x80000000,
            ES_DISPLAY_REQUIRED = 0x00000002,
            ES_SYSTEM_REQUIRED = 0x00000001
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool SystemParametersInfo(
            uint uiAction,
            uint uiParam,
            ref bool pvParam,
            uint fWinIni);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool SystemParametersInfo(
            uint uiAction,
            uint uiParam,
            IntPtr pvParam,
            uint fWinIni);

        private const uint SPI_GETSCREENSAVEACTIVE = 0x0010;
        private const uint SPI_SETSCREENSAVEACTIVE = 0x0011;
        private const uint SPIF_SENDCHANGE = 0x0002;

        private static bool originalScreenSaverState = true;

        public static void BlockWindowsScreenSaver()
        {
            SystemParametersInfo(SPI_GETSCREENSAVEACTIVE, 0, ref originalScreenSaverState, 0);

            SystemParametersInfo(SPI_SETSCREENSAVEACTIVE, 0, IntPtr.Zero, SPIF_SENDCHANGE);

            SetThreadExecutionState(
                EXECUTION_STATE.ES_CONTINUOUS |
                EXECUTION_STATE.ES_DISPLAY_REQUIRED |
                EXECUTION_STATE.ES_SYSTEM_REQUIRED
            );
        }

        public static void RestoreWindowsScreenSaver()
        {
            SystemParametersInfo(
                SPI_SETSCREENSAVEACTIVE,
                originalScreenSaverState ? 1u : 0u,
                IntPtr.Zero,
                SPIF_SENDCHANGE
            );
            SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS);
        }
    }
}