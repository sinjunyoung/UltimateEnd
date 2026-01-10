using System.Collections.Generic;
using UltimateEnd.Models;

namespace UltimateEnd.Managers
{
    public class FavoritesManager
    {
        private static FavoritesManager? _instance;
        private static readonly object _lock = new();

        public static FavoritesManager Instance
        {
            get
            {
                if (_instance == null) lock (_lock) _instance ??= new FavoritesManager();

                return _instance;
            }
        }

        private FavoritesManager() { }

        public static List<GameMetadata> GetFavorites() => AllGamesManager.Instance.GetFavoriteGames();

        public static int Count => AllGamesManager.Instance.GetFavoriteGames().Count;

        public static void Add(GameMetadata game)
        {
            if (game == null) return;

            var existing = AllGamesManager.Instance.GetGame(game.PlatformId, game.RomFile);

            if (existing != null && !existing.IsFavorite)
            {
                existing.IsFavorite = true;
                AllGamesManager.Instance.SavePlatformGames(game.PlatformId);
            }
        }

        public static void Remove(GameMetadata game)
        {
            if (game == null) return;

            var existing = AllGamesManager.Instance.GetGame(game.PlatformId, game.RomFile);

            if (existing != null && existing.IsFavorite)
            {
                existing.IsFavorite = false;
                AllGamesManager.Instance.SavePlatformGames(game.PlatformId);
            }
        }

        public static void Toggle(GameMetadata game)
        {
            if (game == null) return;

            var existing = AllGamesManager.Instance.GetGame(game.PlatformId, game.RomFile);

            if (existing != null)
            {
                existing.IsFavorite = !existing.IsFavorite;
                AllGamesManager.Instance.SavePlatformGames(game.PlatformId);
            }
        }

        public static bool Contains(GameMetadata game)
        {
            if (game == null) return false;

            var existing = AllGamesManager.Instance.GetGame(game.PlatformId, game.RomFile);

            return existing?.IsFavorite ?? false;
        }

        public static void UpdateGame(GameMetadata game)
        {
            if (game == null) return;

            AllGamesManager.Instance.UpdateGame(game);
            AllGamesManager.Instance.SavePlatformGames(game.PlatformId);
        }

        public static void ReloadPlatform(string platformId) => AllGamesManager.Instance.ReloadPlatform(platformId);

        public static void Reload() => AllGamesManager.Instance.Clear();

        public static void Clear() => AllGamesManager.Instance.Clear();
    }
}