using UltimateEnd.Enums;

namespace UltimateEnd.Scraper.Helpers
{
    public static class ScreenScraperSystemClassifier
    {
        public static bool IsArcadeSystem(ScreenScraperSystemId systemId)
        {
            return systemId == ScreenScraperSystemId.MAME ||
                   systemId == ScreenScraperSystemId.NeoGeo ||
                   systemId == ScreenScraperSystemId.SegaNaomi ||
                   systemId == ScreenScraperSystemId.SegaNaomi2 ||
                   systemId == ScreenScraperSystemId.CapcomPlaySystem ||
                   systemId == ScreenScraperSystemId.CapcomPlaySystem2 ||
                   systemId == ScreenScraperSystemId.CapcomPlaySystem3;
        }
    }
}