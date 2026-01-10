using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;

namespace UltimateEnd.Views.Overlays
{
    public partial class GenreFilterOverlay : BaseOverlay
    {
        public event EventHandler<string>? GenreSelected;
        private string? _selectedGenre;
        private List<string> _genres = [];
        private int _selectedIndex = 0;
        private ObservableCollection<GameGenreItem> _genreItems = [];

        public override bool Visible => MainGrid.IsVisible;

        public GenreFilterOverlay()
        {
            InitializeComponent();
            GenreFilterItemsControl.ItemsSource = _genreItems;
        }

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
            for (int i = 0; i < _genreItems.Count; i++)
                _genreItems[i].IsSelected = (i == _selectedIndex);
        }

        public void SetGenres(IEnumerable<string> genres, string? selectedGenre = null)
        {
            _selectedGenre = selectedGenre;
            _genres = [.. genres];

            _selectedIndex = _genres.FindIndex(g => g == selectedGenre);
            if (_selectedIndex < 0) _selectedIndex = 0;

            var items = genres.Select(g => new GameGenreItem
            {
                Genre = g,
                IsSelected = g == selectedGenre
            }).ToList();

            _genreItems = new ObservableCollection<GameGenreItem>(items);

            GenreFilterItemsControl.ItemsSource = _genreItems;
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();
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

                if (GenreFilterItemsControl.ItemsSource is IEnumerable<GameGenreItem> genreItems)
                {
                    foreach (var item in genreItems)
                    {
                        bool newSelectionState = item.Genre == actualGenre;
                        item.IsSelected = newSelectionState;
                    }
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
