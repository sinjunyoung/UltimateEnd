using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class GameFilterService
    {
        private string? _cachedKeyword;
        private string? _lastSearchText;

        public List<GameMetadata> Filter(IEnumerable<GameMetadata> games, string? selectedGenre, string? searchText)
        {
            var filtered = games;

            if (!string.IsNullOrWhiteSpace(selectedGenre) && selectedGenre != "전체")
                filtered = filtered.Where(g => g.Genre == selectedGenre);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                if (_lastSearchText != searchText)
                {
                    _cachedKeyword = searchText.Replace(" ", string.Empty).ToLower();
                    _lastSearchText = searchText;
                }

                var keyword = _cachedKeyword!;
                filtered = filtered.Where(g => g.SearchableText.Contains(keyword));
            }

            return [.. filtered];
        }

        public static List<string> ExtractGenres(IEnumerable<GameMetadata> games)
        {
            var genres = new List<string> { "전체" };

            var uniqueGenres = games
                .Where(g => !string.IsNullOrWhiteSpace(g.Genre))
                .Select(g => g.Genre!)
                .Distinct()
                .OrderBy(g => g);

            genres.AddRange(uniqueGenres);
            return genres;
        }

        public void ClearCache()
        {
            _cachedKeyword = null;
            _lastSearchText = null;
        }
    }
}