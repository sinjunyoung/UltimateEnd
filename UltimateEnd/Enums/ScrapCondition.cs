namespace UltimateEnd.Enums
{
    public enum ScrapCondition
    {
        None = 0,
        LogoMissing = 1 << 0,      // 1
        CoverMissing = 1 << 1,     // 2
        VideoMissing = 1 << 2,     // 4
        AllMediaMissing = LogoMissing | CoverMissing | VideoMissing  // 7
    }
}