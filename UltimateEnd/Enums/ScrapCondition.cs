namespace UltimateEnd.Enums
{
    public enum ScrapCondition
    {
        None = 0,
        LogoMissing = 1 << 0,
        CoverMissing = 1 << 1,
        VideoMissing = 1 << 2,
        AllMediaMissing = LogoMissing | CoverMissing | VideoMissing
    }
}