using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UltimateEnd.Models;

namespace UltimateEnd.Managers
{
    public class GameMetadataManager
    {
        public const string AllGamesKey = "_allgames_";
        public const string FavoritesKey = "_favorites_";
        public const string HistoriesKey = "_histories_";
        public const string SteamKey = "steam";
        public const string AndroidKey = "android";
        public const string DesktopKey = "desktop";

        public static readonly string[] SpecialPlatforms = [AllGamesKey, FavoritesKey, HistoriesKey];

        private readonly HashSet<string> _changedPlatforms = [];
        private readonly Lock _lockObject = new();

        public static bool IsSpecialPlatform(string id) => SpecialPlatforms.Contains(id) || PlaylistManager.IsPlaylistPlatformId(id);

        public static List<GameMetadata> LoadGames(string platformId)
        {
            if (platformId == AllGamesKey)
                return AllGamesManager.Instance.GetAllGames();
            if (platformId == FavoritesKey)
                return AllGamesManager.Instance.GetFavoriteGames();
            if (platformId == HistoriesKey)
                return AllGamesManager.Instance.GetHistoryGames();
            if (platformId == AndroidKey)
                return AllGamesManager.Instance.GetPlatformGames(AndroidKey);
            if (platformId == DesktopKey)
                return AllGamesManager.Instance.GetPlatformGames(DesktopKey);

            if (PlaylistManager.IsPlaylistPlatformId(platformId))
            {
                var playlistId = PlaylistManager.ExtractPlaylistId(platformId);
                return PlaylistManager.Instance.GetPlaylistGames(playlistId);
            }

            return AllGamesManager.Instance.GetPlatformGames(platformId);
        }

        public void MarkPlatformAsChanged(string? platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            lock (_lockObject) _changedPlatforms.Add(platformId);
        }

        public bool HasChangedPlatforms()
        {
            lock (_lockObject) return _changedPlatforms.Count > 0;
        }

        public void SaveGames()
        {
            HashSet<string> platformsToSave;

            lock (_lockObject)
            {
                if (_changedPlatforms.Count == 0) return;

                platformsToSave = [.. _changedPlatforms];
            }

            foreach (var platformId in platformsToSave) AllGamesManager.Instance.SavePlatformGames(platformId);

            lock (_lockObject) 
                foreach (var platformId in platformsToSave) 
                    _changedPlatforms.Remove(platformId);
        }

        public void ForceSave(string platformId)
        {
            if (string.IsNullOrEmpty(platformId)) return;

            AllGamesManager.Instance.SavePlatformGames(platformId);
        }

        public void ClearChangedPlatforms()
        {
            lock (_lockObject) _changedPlatforms.Clear();
        }
    }
}