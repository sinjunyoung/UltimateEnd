using System;

namespace UltimateEnd.SaveFile.CHD
{
    [Flags]
    public enum ChdrOpenFlags
    {
        CHDOPEN_READ = 1,
        CHDOPEN_READWRITE = 2
    }
}