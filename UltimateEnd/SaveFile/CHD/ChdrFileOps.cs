using System;
using System.Runtime.InteropServices;

namespace UltimateEnd.SaveFile.CHD
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ChdrFileOps
    {
        public IntPtr fsize;
        public IntPtr fread;
        public IntPtr fclose;
        public IntPtr fseek;
    }
}