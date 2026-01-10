using UltimateEnd.Models;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper.Helpers
{
    public static class MetadataApplier
    {
        public static bool ApplyScrapedMetadata(GameMetadata game, GameResult scrapedGame, bool isArcade, string romPath)
        {
            if (scrapedGame == null)
                return false;

            bool updated = false;

            if (isArcade)
            {
                var dbGame = FbNeoGameDatabase.GetGameByPath(romPath);

                if (dbGame != null)
                {
                    if (!string.IsNullOrEmpty(dbGame.Title))
                    {
                        game.Title = dbGame.Title;
                        updated = true;
                    }

                    if (!string.IsNullOrEmpty(dbGame.Description))
                    {
                        game.Description = dbGame.Description;
                        updated = true;
                    }

                    if (game.HasKorean != dbGame.IsKorean)
                    {
                        game.HasKorean = dbGame.IsKorean;
                        updated = true;
                    }

                    if (!string.IsNullOrEmpty(scrapedGame.Genre))
                    {
                        game.Genre = scrapedGame.Genre;
                        updated = true;
                    }

                    if (!string.IsNullOrEmpty(scrapedGame.Developer))
                    {
                        game.Developer = scrapedGame.Developer;
                        updated = true;
                    }

                    if (updated)
                        return updated;
                }
            }

            if (string.IsNullOrEmpty(game.Title) || (ScreenScraperConfig.Instance.AllowScrapTitle && !string.IsNullOrEmpty(scrapedGame.Title)))
            {
                game.Title = scrapedGame.Title;
                updated = true;
            }

            if (string.IsNullOrEmpty(game.Description) || (ScreenScraperConfig.Instance.AllowScrapDescription && !string.IsNullOrEmpty(scrapedGame.Description)))
            {
                game.Description = scrapedGame.Description;
                updated = true;
            }

            if (!string.IsNullOrEmpty(scrapedGame.Genre))
            {
                game.Genre = scrapedGame.Genre;
                updated = true;
            }

            if (!string.IsNullOrEmpty(scrapedGame.Developer))
            {
                game.Developer = scrapedGame.Developer;
                updated = true;
            }

            return updated;
        }
    }
}