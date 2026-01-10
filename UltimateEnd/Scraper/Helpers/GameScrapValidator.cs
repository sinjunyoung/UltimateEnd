using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Scraper.Helpers
{
    internal static class GameScrapValidator
    {
        public static bool ShouldSkipGame(GameMetadata game, ScrapCondition condition)
        {
            return condition switch
            {
                ScrapCondition.None => false,
                ScrapCondition.LogoMissing => game.HasLogoImage,
                ScrapCondition.CoverMissing => game.HasCoverImage,
                ScrapCondition.VideoMissing => game.HasVideo,
                ScrapCondition.AllMediaMissing => game.HasLogoImage || game.HasCoverImage || game.HasVideo,
                _ => false
            };
        }
    }
}