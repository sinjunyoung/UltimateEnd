using Avalonia.Threading;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Extractor;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Managers
{
    public class GameCollectionManager : IDisposable
    {
        private readonly GameFilterService _filterService;
        private readonly GameMetadataManager _metadataManager;
        private readonly ObservableCollection<GameMetadata> _allGames = [];
        private readonly ObservableCollection<string> _genres = [];
        private readonly ObservableCollection<GameGenreItem> _editingGenres = [];
        private readonly List<GameMetadata> _subscribedGames = [];
        private ExtractMetadataEnricher _metadataExtractor;

        private string _selectedGenre = "전체";
        private string _searchText = string.Empty;
        private GameMetadata? _selectedGame;
        private bool _disposed;
        private bool _isShowingDeletedGames = false;

        public ObservableCollection<GameMetadata> Games { get; }

        public ObservableCollection<string> Genres => _genres;

        public ObservableCollection<GameGenreItem> EditingGenres => _editingGenres;

        public bool IsShowingDeletedGames
        {
            get => _isShowingDeletedGames;
            set
            {
                if (_isShowingDeletedGames != value)
                {
                    _isShowingDeletedGames = value;
                    FilterGames();
                    ShowingDeletedGamesChanged?.Invoke(value);                    
                }
            }
        }

        public string SelectedGenre
        {
            get => _selectedGenre;
            set
            {
                _selectedGenre = value;
                FilterGames();
                SelectedGenreChanged?.Invoke(value);                
            }
        }

        public string CommittedSearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                SearchTextChanged?.Invoke(value);
                FilterGames();
            }
        }

        public GameMetadata? SelectedGame
        {
            get => _selectedGame;
            set
            {
                if (_selectedGame != null) _selectedGame.IsSelected = false;

                _selectedGame = value;
                SelectedGameChanged?.Invoke(value);

                if (value != null) value.IsSelected = true;
            }
        }

        public event Action<bool>? ShowingDeletedGamesChanged;
        public event Action<string>? SelectedGenreChanged;
        public event Action<string>? SearchTextChanged;
        public event Action<GameMetadata?>? SelectedGameChanged;
        public event Action<GameMetadata>? GamePropertyChanged;

        public GameCollectionManager()
        {
            Games = [];
            _filterService = new GameFilterService();
            _metadataManager = new GameMetadataManager();
        }

        public void LoadGames(string platformId)
        {
            UnsubscribeAllGames();
            ClearCollections();
            var metadata = GameMetadataManager.LoadGames(platformId);
            var sortedGames = SortGames([.. metadata], platformId);

            foreach (var game in sortedGames)
            {
                _allGames.Add(game);
                SubscribeToGame(game);
            }

            _ = Task.Run(async () =>
            {
                _metadataExtractor?.Cancel();
                _metadataExtractor = new ExtractMetadataEnricher(platformId);

                int maxParallel = OperatingSystem.IsAndroid() ? 1 : 4;

                await _metadataExtractor.ExtractInBackground(platformId, sortedGames, maxParallel);
            });

            FilterGames();
        }

        private void ClearCollections()
        {
            foreach (var game in _allGames) game.Dispose();

            _allGames.Clear();
            Games.Clear();
        }

        private static List<GameMetadata> SortGames(List<GameMetadata> games, string platformId)
        {
            if (platformId == GameMetadataManager.HistoriesKey) return games;

            return [.. games
                .OrderBy(g => KoreanStringComparer.HasKorean(g.Title) ? 0 : 1)
                .ThenBy(g => g.Title!, new KoreanStringComparer())];
        }

        private void SubscribeToGame(GameMetadata game)
        {
            game.PropertyChanged += Game_PropertyChanged;
            _subscribedGames.Add(game);
        }

        public void LoadGenres()
        {
            _genres.Clear();

            var gamesForGenre = _allGames.Where(g => _isShowingDeletedGames ? g.Ignore : !g.Ignore);
            var genres = GameFilterService.ExtractGenres(gamesForGenre);

            foreach (var genre in genres) _genres.Add(genre);

            _selectedGenre = "전체";
            LoadEditingGenres();
        }

        private void LoadEditingGenres()
        {
            var genresFromFile = GenreService.LoadGenres();

            _editingGenres.Clear();
            _editingGenres.Add(new GameGenreItem { Id = 0, Genre = string.Empty });

            var distinctGenres = genresFromFile
                .GroupBy(kvp => kvp.Value)
                .Select(g => g.OrderBy(kvp => kvp.Key).First())
                .OrderBy(kvp => kvp.Value);

            foreach (var kvp in distinctGenres) _editingGenres.Add(new GameGenreItem { Id = kvp.Key, Genre = kvp.Value });
        }

        private void FilterGames()
        {
            IEnumerable<GameMetadata> source;

            source = _allGames.Where(g => _isShowingDeletedGames ? g.Ignore : !g.Ignore);
            var filtered = _filterService.Filter(source, SelectedGenre, _searchText);
            var currentSelection = SelectedGame;

            if (Games.Count == 0) UpdateFilteredGames(filtered, currentSelection);
            else Dispatcher.UIThread.Post(() => UpdateFilteredGames(filtered, currentSelection));
        }

        private void UpdateFilteredGames(List<GameMetadata> filtered, GameMetadata? currentSelection)
        {
            Games.Clear();
            Games.AddRange(filtered);

            if (currentSelection != null && Games.Contains(currentSelection))
                SelectedGame = currentSelection;
            else if (Games.Any())
                SelectedGame = Games.First();
        }

        private void Game_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_disposed) return;

            if (sender is GameMetadata game)
            {
                if (e.PropertyName == nameof(GameMetadata.Title) ||
                    e.PropertyName == nameof(GameMetadata.Description) ||
                    e.PropertyName == nameof(GameMetadata.Developer) ||
                    e.PropertyName == nameof(GameMetadata.Genre) ||
                    e.PropertyName == nameof(GameMetadata.IsFavorite) ||
                    e.PropertyName == nameof(GameMetadata.Ignore) ||
                    e.PropertyName == nameof(GameMetadata.HasKorean) ||
                    e.PropertyName == nameof(GameMetadata.EmulatorId) ||
                    e.PropertyName == nameof(GameMetadata.CoverImagePath) ||
                    e.PropertyName == nameof(GameMetadata.LogoImagePath) ||
                    e.PropertyName == nameof(GameMetadata.VideoPath) ||
                    e.PropertyName == nameof(GameMetadata.ScrapHint))

                {
                    GamePropertyChanged?.Invoke(game);
                }

                if (e.PropertyName == nameof(GameMetadata.Genre) || e.PropertyName == nameof(GameMetadata.Ignore))
                    FilterGames();
            }
        }

        private void UnsubscribeAllGames()
        {
            foreach (var game in _subscribedGames) game.PropertyChanged -= Game_PropertyChanged;

            _subscribedGames.Clear();
        }

        public List<GameMetadata> GetAllGames() => [.. _allGames];

        public int GetDeletedGamesCount() => _allGames.Count(g => g.Ignore);

        public void ClearCache() => _filterService.ClearCache();

        private HashSet<GameMetadata> UpdateFromExternalMetadataInternal(string platformId, string path, Func<ObservableCollection<GameMetadata>, string, string, CancellationToken, HashSet<GameMetadata>> updateFunc, CancellationToken cancellationToken)
        {
            foreach (var game in _subscribedGames) game.PropertyChanged -= Game_PropertyChanged;

            try
            {
                var changedGames = updateFunc(_allGames, platformId, path, cancellationToken);
                var newGames = _allGames.Except(_subscribedGames).ToList();

                foreach (var game in newGames)
                {
                    game.PropertyChanged += Game_PropertyChanged;
                    _subscribedGames.Add(game);
                }

                FilterGames();

                return changedGames;
            }
            finally
            {
                foreach (var game in _subscribedGames) game.PropertyChanged += Game_PropertyChanged;
            }
        }

        public HashSet<GameMetadata> UpdateFromPegasusMetadata(string platformId, string path, CancellationToken cancellationToken = default) => UpdateFromExternalMetadataInternal(platformId, path, MetadataService.UpdateFromPegasusMetadata, cancellationToken);

        public HashSet<GameMetadata> UpdateFromEsDeMetadata(string platformId, string path, CancellationToken cancellationToken = default) => UpdateFromExternalMetadataInternal(platformId, path, MetadataService.UpdateFromEsDeMetadata, cancellationToken);

        public static void AddToPlaylist(string playlistId, GameMetadata game) => PlaylistManager.Instance.AddGameToPlaylist(playlistId, game);

        public void RemoveFromPlaylist(string playlistId, GameMetadata game)
        {
            if (game.PlatformId == null) return;

            PlaylistManager.Instance.RemoveGameFromPlaylist(playlistId, game.PlatformId, game.RomFile);

            FilterGames();
        }

        public static List<Playlist> GetPlaylistsForGame(GameMetadata game)
        {
            if (game.PlatformId == null) return [];

            return PlaylistManager.Instance.GetPlaylistsContainingGame(game.PlatformId, game.RomFile);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            UnsubscribeAllGames();
            _filterService.ClearCache();

            foreach (var game in _allGames) game.Dispose();

            _allGames.Clear();
        }
    }
}