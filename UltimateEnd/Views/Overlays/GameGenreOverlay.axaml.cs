using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Views.Overlays
{
    public partial class GameGenreOverlay : BaseOverlay
    {
        public event EventHandler<string>? GenreSelected;
        private string? _selectedGenre;
        private List<string> _genres = [];
        private int _selectedIndex = 0;

        public override bool Visible => MainGrid.IsVisible;

        public GameGenreOverlay() => InitializeComponent();

        protected override void MovePrevious()
        {
            if (_genres.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _genres.Count) % _genres.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_genres.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _genres.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_genres.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _genres.Count)
            {
                var selected = _genres[_selectedIndex];
                GenreSelected?.Invoke(this, selected);
            }
        }

        private void UpdateSelection()
        {
            var borders = GameGenreItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.DataContext is GameGenreItem)
                .ToList();

            if (borders == null || borders.Count == 0) return;

            for (int i = 0; i < borders.Count; i++)
            {
                var border = borders[i];
                if (i == _selectedIndex)
                {
                    border.Background = this.FindResource("Background.Hover") as IBrush;
                    border.BringIntoView();
                }
                else
                    border.Background = Brushes.Transparent;
            }
        }

        public void SetGenres(IEnumerable<GameGenreItem> genres, string? selectedGenre = null)
        {
            _selectedGenre = selectedGenre;

            var distinctGenres = genres
                .GroupBy(g => g.Genre)
                .Select(group => group.First())
                .ToList();

            _genres = [.. distinctGenres.Select(g => g.Genre)];

            _selectedIndex = _genres.FindIndex(g => g == selectedGenre);
            if (_selectedIndex < 0) _selectedIndex = 0;

            var gameGenreItems = genres.Select(g => new GameGenreItem
            {
                Id = g.Id,
                Genre = g.Genre,
                IsSelected = g.Genre == selectedGenre
            }).ToList();

            GameGenreItemsControl.ItemsSource = gameGenreItems;
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateSelection(),
                Avalonia.Threading.DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            MainGrid.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnGenreItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is GameGenreItem tappedItem)
            {
                _selectedIndex = _genres.IndexOf(tappedItem.Genre);
                var actualGenre = tappedItem.Genre;
                _selectedGenre = actualGenre;
                GenreSelected?.Invoke(this, actualGenre);

                if (GameGenreItemsControl.ItemsSource is IEnumerable<GameGenreItem> genreItems)
                {
                    foreach (var item in genreItems)
                    {
                        bool newSelectionState = item.Genre == actualGenre;
                        item.IsSelected = newSelectionState;
                    }
                    var currentSource = GameGenreItemsControl.ItemsSource;
                    GameGenreItemsControl.ItemsSource = null;
                    GameGenreItemsControl.ItemsSource = currentSource;
                }
                Hide(HiddenState.Confirm);
            }
            e.Handled = true;
        }

        private void OnClose(object? sender, PointerPressedEventArgs e)
        {
            Hide(HiddenState.Close);
            e.Handled = true;
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Hide(HiddenState.Close);
        }
    }
}