using System;
using System.Linq;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper
{
    public class GameMatcher
    {
        private static readonly string[] TrustedDevelopers = ["enix", "square", "nintendo", "capcom", "konami", "namco", "chunsoft"];
        private static readonly string[] Blacklist = ["yong zhe", "waixing", "mars production", "hack", "bootleg", "pirate"];

        public static int CalculateMatchScore(string searchTerm, GameResult game)
        {
            if (string.IsNullOrWhiteSpace(searchTerm) || game == null)
                return 0;

            int score = 0;
            var search = NormalizeText(searchTerm);
            var title = NormalizeText(game.Title);

            score += CalculateNumberMatch(search, title);

            if (title.Contains(search))
                score += 500;
            else if (title.StartsWith(search))
                score += 300;

            score += CalculateWordMatch(search, title);

            if (search.Length > 0 && title.Length > 0 && search[0] == title[0])
                score += 50;

            score += CalculateReleaseDateBonus(game.ReleaseDate);
            score += CalculateDeveloperTrust(game.Developer);
            score += ApplyBlacklist(title, game.Developer);

            return Math.Max(0, score);
        }

        private static string NormalizeText(string text) => text?.ToLower().Trim() ?? string.Empty;

        private static int CalculateNumberMatch(string search, string title)
        {
            var searchNumbers = System.Text.RegularExpressions.Regex.Matches(search, @"\d+");
            var titleNumbers = System.Text.RegularExpressions.Regex.Matches(title, @"\d+");

            if (searchNumbers.Count == 0 || titleNumbers.Count == 0)
                return 0;

            foreach (System.Text.RegularExpressions.Match searchNum in searchNumbers)
            {
                foreach (System.Text.RegularExpressions.Match titleNum in titleNumbers)
                {
                    if (searchNum.Value == titleNum.Value)
                    {
                        int positionDiff = Math.Abs(searchNum.Index - titleNum.Index);
                        int bonus = positionDiff < 5 ? 200 : 0;
                        return 1000 + bonus;
                    }
                }
            }

            return 0;
        }

        private static int CalculateWordMatch(string search, string title)
        {
            var searchWords = search.Split([' ', '-', '_', ':', '.'],
                StringSplitOptions.RemoveEmptyEntries);

            int score = 0;

            foreach (var word in searchWords)
            {
                if (word.Length < 2) continue;

                if (title.Contains(word))
                    score += 100;
            }

            return score;
        }

        private static int CalculateReleaseDateBonus(string releaseDate)
        {
            if (string.IsNullOrEmpty(releaseDate) || releaseDate.Length < 4)
                return 0;

            if (int.TryParse(releaseDate.AsSpan(0, 4), out int year))
            {
                if (year >= 1980 && year <= 2010)
                    return (2010 - year) / 2;
            }

            return 0;
        }

        private static int CalculateDeveloperTrust(string developer)
        {
            if (string.IsNullOrEmpty(developer))
                return 0;

            var dev = developer.ToLower();
            return TrustedDevelopers.Any(t => dev.Contains(t)) ? 200 : 0;
        }

        private static int ApplyBlacklist(string title, string developer)
        {
            foreach (var bad in Blacklist)
                if (title.Contains(bad) || (developer?.ToLower().Contains(bad) ?? false))
                    return -500;

            return 0;
        }
    }
}