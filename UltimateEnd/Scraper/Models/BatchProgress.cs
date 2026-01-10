using UltimateEnd.Models;

namespace UltimateEnd.Scraper.Models
{
    public class BatchProgress(int current, int total, int success, int failed, int cached, int skipped, string status, GameMetadata successGame)
    {
        public int Current { get; } = current;

        public int Total { get; } = total;

        public int Success { get; } = success;

        public int Failed { get; } = failed;

        public int Cached { get; } = cached;

        public int Skipped { get; } = skipped;

        public string Status { get; } = status;

        public GameMetadata SuccessGame { get; } = successGame;
    }
}