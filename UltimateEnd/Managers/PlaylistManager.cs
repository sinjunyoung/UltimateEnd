using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Managers
{
    public class PlaylistManager
    {
        private static PlaylistManager? _instance;
        private static readonly Lock _lock = new();

        private readonly List<Playlist> _playlists = [];
        private readonly Lock _playlistsLock = new();

        public const string PlaylistPrefix = "_playlist_";

        public static PlaylistManager Instance
        {
            get
            {
                if (_instance == null) lock (_lock) _instance ??= new PlaylistManager();
                return _instance;
            }
        }

        private PlaylistManager()
        {
            LoadPlaylists();
        }

        public static event Action? PlaylistsChanged;

        private void LoadPlaylists()
        {
            lock (_playlistsLock)
            {
                _playlists.Clear();
                var playlists = PlaylistService.LoadPlaylists();
                _playlists.AddRange(playlists);
            }
        }

        public List<Playlist> GetAllPlaylists()
        {
            lock (_playlistsLock) return [.. _playlists];
        }

        public Playlist? GetPlaylist(string playlistId)
        {
            lock (_playlistsLock) return _playlists.FirstOrDefault(p => p.Id == playlistId);
        }

        public Playlist CreatePlaylist(string name)
        {
            var playlist = new Playlist
            {
                Id = Guid.NewGuid().ToString(),
                Name = name
            };

            lock (_playlistsLock)
            {
                _playlists.Add(playlist);
                SavePlaylists();
            }

            AllGamesManager.Instance.InvalidatePlatformCache();
            PlaylistsChanged?.Invoke();

            return playlist;
        }

        public void UpdatePlaylist(Playlist playlist)
        {
            lock (_playlistsLock)
            {
                var existing = _playlists.FirstOrDefault(p => p.Id == playlist.Id);

                if (existing != null)
                {
                    var index = _playlists.IndexOf(existing);
                    _playlists[index] = playlist;
                    SavePlaylists();
                    PlaylistsChanged?.Invoke();
                }
            }
        }

        public void DeletePlaylist(string playlistId)
        {
            lock (_playlistsLock)
            {
                var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);

                if (playlist != null)
                {
                    _playlists.Remove(playlist);
                    SavePlaylists();
                    AllGamesManager.Instance.InvalidatePlatformCache();
                    PlaylistsChanged?.Invoke();
                }
            }
        }

        public void AddGameToPlaylist(string playlistId, GameMetadata game)
        {
            if (game.PlatformId == null) return;

            lock (_playlistsLock)
            {
                var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);

                if (playlist == null) return;

                if (playlist.GameReferences.Any(r => r.BasePath == game.GetBasePath() && r.RomFile == game.RomFile)) return;

                var reference = new PlaylistGameReference
                {
                    PlatformId = game.PlatformId,
                    BasePath = game.GetBasePath(),
                    RomFile = game.RomFile,
                    Order = playlist.GameReferences.Count
                };

                playlist.GameReferences.Add(reference);

                SavePlaylists();
                AllGamesManager.Instance.InvalidatePlatformCache();
                PlaylistsChanged?.Invoke();
            }
        }

        public void RemoveGameFromPlaylist(string playlistId, string platformId, string romFile)
        {
            lock (_playlistsLock)
            {
                var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);

                if (playlist == null) return;

                var reference = playlist.GameReferences.FirstOrDefault(r => r.PlatformId == platformId && r.RomFile == romFile);

                if (reference != null)
                {
                    playlist.GameReferences.Remove(reference);

                    for (int i = 0; i < playlist.GameReferences.Count; i++) playlist.GameReferences[i].Order = i;

                    SavePlaylists();
                    AllGamesManager.Instance.InvalidatePlatformCache();
                    PlaylistsChanged?.Invoke();
                }
            }
        }

        public void ReorderPlaylistItems(string playlistId, int oldIndex, int newIndex)
        {
            if (oldIndex == newIndex) return;

            lock (_playlistsLock)
            {
                var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);

                if (playlist == null) return;

                var reference = playlist.GameReferences[oldIndex];
                playlist.GameReferences.RemoveAt(oldIndex);
                playlist.GameReferences.Insert(newIndex, reference);

                for (int i = 0; i < playlist.GameReferences.Count; i++) playlist.GameReferences[i].Order = i;

                SavePlaylists();
                PlaylistsChanged?.Invoke();
            }
        }

        public List<GameMetadata> GetPlaylistGames(string playlistId)
        {
            lock (_playlistsLock)
            {
                var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);

                if (playlist == null) return [];

                var games = new List<GameMetadata>();

                foreach (var reference in playlist.GameReferences.OrderBy(r => r.Order))
                {
                    var allGames = AllGamesManager.Instance.GetAllGames();
                    var game = allGames.FirstOrDefault(g => g.GetBasePath() == reference.BasePath && g.RomFile == reference.RomFile);

                    if (game != null) games.Add(game);
                }

                return games;
            }
        }

        public bool IsGameInPlaylist(string playlistId, string platformId, string romFile)
        {
            lock (_playlistsLock)
            {
                var playlist = _playlists.FirstOrDefault(p => p.Id == playlistId);

                return playlist?.GameReferences.Any(r => r.PlatformId == platformId && r.RomFile == romFile) ?? false;
            }
        }

        public List<Playlist> GetPlaylistsContainingGame(string platformId, string romFile)
        {
            lock (_playlistsLock) return [.. _playlists.Where(p => p.GameReferences.Any(r => r.PlatformId == platformId && r.RomFile == romFile))];
        }

        public static string GetPlaylistPlatformId(string playlistId) => $"{PlaylistPrefix}{playlistId}";

        public static bool IsPlaylistPlatformId(string platformId) => platformId.StartsWith(PlaylistPrefix);

        public static string ExtractPlaylistId(string platformId)
        {
            if (IsPlaylistPlatformId(platformId)) return platformId[PlaylistPrefix.Length..];

            return platformId;
        }

        private void SavePlaylists() => PlaylistService.SavePlaylists(_playlists);

        public void Clear()
        {
            lock (_playlistsLock) _playlists.Clear();
        }
    }
}