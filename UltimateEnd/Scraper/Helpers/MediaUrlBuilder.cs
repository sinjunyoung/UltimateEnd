using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper.Helpers
{
    public static class MediaUrlBuilder
    {
        public static string BuildMediaUrl(int systemId, int gameId, string mediaType)
        {
            var config = ScreenScraperConfig.Instance;

            return $"https://neoclone.screenscraper.fr/api2/mediaJeu.php?" +
                   $"devid={config.ApiDevU}&devpassword={config.ApiDevP}" +
                   $"&softname=UltimateEnd&ssid=&sspassword=" +
                   $"&systemeid={systemId}&jeuid={gameId}&media={mediaType}";
        }

        public static string BuildVideoUrl(int systemId, int gameId, string mediaType)
        {
            var config = ScreenScraperConfig.Instance;

            return $"https://neoclone.screenscraper.fr/api2/mediaVideoJeu.php?" +
                   $"devid={config.ApiDevU}&devpassword={config.ApiDevP}" +
                   $"&softname=UltimateEnd&ssid=&sspassword=" +
                   $"&systemeid={systemId}&jeuid={gameId}&media={mediaType}";
        }
    }
}