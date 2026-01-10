using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Helpers;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class GenreEditorOverlay : BaseOverlay
    {
        public event EventHandler<Dictionary<int, string>>? GenresSaved;

        private readonly ObservableCollection<GameGenreItem> _genres = [];
        private ObservableCollection<GameGenreItem> _filteredGenres = [];
        private GameGenreItem? _selectedItem = null;
        private GameGenreItem? _renamingItem = null;
        private Dictionary<int, string> _originalGenres = [];
        private string _searchFilter = string.Empty;

        public override bool Visible => this.IsVisible;

        public GenreEditorOverlay()
        {
            InitializeComponent();

            this.IsVisible = false;

            GenreItemsControl.ItemsSource = _filteredGenres;

            if (SearchTextBox != null)
            {
                SearchTextBox.TextChanged += OnSearchTextChanged;
                SearchTextBox.KeyDown += OnSearchTextBoxKeyDown;
            }

            this.AttachedToVisualTree += (s, e) =>
            {
                if (this.IsVisible && SearchOverlay?.IsVisible == true)
                    SearchTextBox?.Focus();
            };

            this.KeyDown += HandleKeyDown;
        }

        private void HandleKeyDown(object? sender, KeyEventArgs e)
        {
            if (SearchOverlay?.IsVisible == true || RenameOverlay?.IsVisible == true)
                return;

            if (e.Key == Key.Delete && _selectedItem != null)
            {
                e.Handled = true;
                DeleteGenre(_selectedItem);
                return;
            }

            OnKeyDown(e);
        }

        protected override void MovePrevious() => MoveSelection(-1);

        protected override void MoveNext() => MoveSelection(1);

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var text = textBox.Text?.Trim() ?? string.Empty;

                if (!string.IsNullOrEmpty(text))
                    SearchStatusText.Text = "검색 또는 추가 버튼을 누르세요";
                else
                    SearchStatusText.Text = "장르 이름을 입력하세요";
            }
        }

        private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;
                PerformSearch();
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                e.Handled = true;
                HideSearchOverlay();
            }
        }

        private void OnSearchConfirm(object? sender, RoutedEventArgs e) => PerformSearch();

        private void OnAddConfirm(object? sender, RoutedEventArgs e) => AddGenreFromSearch();

        private void PerformSearch()
        {
            var searchText = SearchTextBox?.Text?.Trim() ?? string.Empty;

            _searchFilter = searchText;
            ApplyFilter();

            if (!string.IsNullOrEmpty(searchText))
            {
                var matchCount = _filteredGenres.Count;

                if (matchCount > 0)
                    SearchStatusText.Text = $"{matchCount}개의 장르가 검색되었습니다";
                else
                    SearchStatusText.Text = "검색 결과가 없습니다. 추가 버튼을 눌러 새 장르를 만드세요";
            }
            else
                SearchStatusText.Text = "전체 장르 목록입니다";

            HideSearchOverlay();
        }

        private void AddGenreFromSearch()
        {
            var newGenreName = SearchTextBox?.Text?.Trim();

            if (string.IsNullOrEmpty(newGenreName))
            {
                SearchStatusText.Text = "추가할 장르 이름을 입력하세요";
                return;
            }

            var isDuplicate = _genres.Any(g =>
                g.Genre.Equals(newGenreName, StringComparison.OrdinalIgnoreCase));

            if (isDuplicate)
            {
                SearchStatusText.Text = $"'{newGenreName}' 장르는 이미 존재합니다";
                return;
            }

            var newId = GenreService.GetNextAvailableId();
            var newItem = new GameGenreItem { Id = newId, Genre = newGenreName, IsSelected = false };
            _genres.Add(newItem);

            SortGenres();

            _searchFilter = string.Empty;
            ApplyFilter();

            SearchTextBox.Text = string.Empty;
            SearchStatusText.Text = $"'{newGenreName}' 장르가 추가되었습니다";

            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await System.Threading.Tasks.Task.Delay(800);
                HideSearchOverlay();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        private void ApplyFilter()
        {
            var filtered = string.IsNullOrEmpty(_searchFilter)
                ? _genres
                : _genres.Where(g =>
                    g.Genre.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));

            var items = filtered.Select(g => g).ToList();
            _filteredGenres = new ObservableCollection<GameGenreItem>(items);
            GenreItemsControl.ItemsSource = _filteredGenres;
        }

        private void MoveSelection(int direction)
        {
            if (_filteredGenres.Count == 0) return;

            int currentIndex = _selectedItem != null ? _filteredGenres.IndexOf(_selectedItem) : -1;
            int newIndex = currentIndex + direction;

            if (newIndex < 0) newIndex = 0;
            if (newIndex >= _filteredGenres.Count) newIndex = _filteredGenres.Count - 1;

            if (newIndex >= 0 && newIndex < _filteredGenres.Count)
            {
                UpdateSelection(_filteredGenres[newIndex]);
            }
        }

        public void LoadGenres(Dictionary<int, string> genres)
        {
            _originalGenres = new Dictionary<int, string>(genres);

            _genres.Clear();

            foreach (var kvp in _originalGenres.OrderBy(g => g.Value))
                _genres.Add(new GameGenreItem { Id = kvp.Key, Genre = kvp.Value, IsSelected = false });

            ApplyFilter();

            if (_filteredGenres.Count > 0)
            {
                _selectedItem = _filteredGenres[0];
                _selectedItem.IsSelected = true;
            }
            else
                _selectedItem = null;
        }

        private async void OnGenreItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is GameGenreItem tappedItem)
            {
                await WavSounds.OK();
                UpdateSelection(tappedItem);
            }
            e.Handled = true;
        }

        private void OnEditButtonClick(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is Button button && button.DataContext is GameGenreItem item)
                ShowRenameOverlay(item);
        }

        private void OnDeleteButtonClick(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is Button button && button.DataContext is GameGenreItem item)
                DeleteGenre(item);
        }

        private void DeleteGenre(GameGenreItem item)
        {
            _genres.Remove(item);
            ApplyFilter();

            if (_selectedItem == item)
            {
                _selectedItem = _filteredGenres.FirstOrDefault();
                if (_selectedItem != null)
                    _selectedItem.IsSelected = true;
            }
        }

        private void ShowRenameOverlay(GameGenreItem item)
        {
            if (RenameOverlay != null && RenameTextBox != null && RenameStatusText != null)
            {
                _renamingItem = item;
                RenameTextBox.Text = item.Genre;
                RenameStatusText.Text = $"'{item.Genre}'의 새 이름을 입력하세요";

                RenameOverlay.IsVisible = true;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    RenameTextBox.Focus();
                    RenameTextBox.SelectAll();
                }, Avalonia.Threading.DispatcherPriority.Loaded);

                if (RenameTextBox != null)
                {
                    RenameTextBox.KeyDown -= OnRenameTextBoxKeyDown;
                    RenameTextBox.KeyDown += OnRenameTextBoxKeyDown;
                }
            }
        }

        private void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;
                ConfirmRename();
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                e.Handled = true;
                HideRenameOverlay();
            }
        }

        private void HideRenameOverlay()
        {
            if (RenameOverlay != null)
            {
                RenameOverlay.IsVisible = false;
                _renamingItem = null;

                if (RenameTextBox != null)
                    RenameTextBox.KeyDown -= OnRenameTextBoxKeyDown;

                Avalonia.Threading.Dispatcher.UIThread.Post(() => this.Focus(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnRenameOverlayClose(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            HideRenameOverlay();
        }

        private void OnRenameOverlayBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                HideRenameOverlay();
        }

        private void OnRenameCancel(object? sender, RoutedEventArgs e) => HideRenameOverlay();

        private void OnRenameConfirm(object? sender, RoutedEventArgs e) => ConfirmRename();

        private void ConfirmRename()
        {
            var newName = RenameTextBox?.Text?.Trim();

            if (string.IsNullOrEmpty(newName))
            {
                if (RenameStatusText != null)
                    RenameStatusText.Text = "새 이름을 입력하세요";
                return;
            }

            if (_renamingItem != null)
            {
                var isDuplicateName = _genres.Any(g =>
                    g != _renamingItem &&
                    g.Genre.Equals(newName, StringComparison.OrdinalIgnoreCase));

                if (isDuplicateName)
                {
                    if (RenameStatusText != null)
                        RenameStatusText.Text = $"'{newName}' 장르는 이미 존재합니다";
                    return;
                }

                _renamingItem.Genre = newName;

                SortGenres();

                ApplyFilter();

                HideRenameOverlay();
            }
        }

        private void SortGenres()
        {
            var sortedList = _genres.OrderBy(g => g.Genre).ToList();
            _genres.Clear();
            foreach (var item in sortedList)
                _genres.Add(item);
        }

        private async void OnSearchClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.OK();
            ShowSearchOverlay();
        }

        private void ShowSearchOverlay()
        {
            if (SearchOverlay != null && SearchTextBox != null)
            {
                SearchTextBox.Text = _searchFilter;

                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    var matchCount = _filteredGenres.Count;
                    SearchStatusText.Text = matchCount > 0
                        ? $"{matchCount}개의 장르가 검색되었습니다"
                        : "검색 결과가 없습니다";
                }
                else
                {
                    SearchStatusText.Text = "장르 이름을 입력하세요";
                }

                SearchOverlay.IsVisible = true;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    SearchTextBox.Focus();
                    SearchTextBox.SelectAll();
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void HideSearchOverlay()
        {
            if (SearchOverlay != null)
            {
                SearchOverlay.IsVisible = false;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => this.Focus(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnSearchOverlayClose(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            HideSearchOverlay();
        }

        private void OnSearchOverlayBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                HideSearchOverlay();
        }

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            var finalGenres = _genres
                .Where(g => !string.IsNullOrEmpty(g.Genre.Trim()))
                .GroupBy(g => g.Id)
                .Select(group => group.First())
                .ToDictionary(g => g.Id, g => g.Genre.Trim());

            GenreService.SaveGenres(finalGenres);

            GenresSaved?.Invoke(this, finalGenres);

            Hide(HiddenState.Confirm);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e) => Hide(HiddenState.Cancel);

        private void UpdateSelection(GameGenreItem? newItem)
        {
            foreach (var item in _genres)
                item.IsSelected = (item == newItem);

            _selectedItem = newItem;
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            var genres = GenreService.LoadGenres();
            LoadGenres(genres);

            this.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            if (MainGrid != null)
                MainGrid.IsVisible = true;
        }

        public override void Hide(HiddenState state)
        {
            HideSearchOverlay();
            HideRenameOverlay();

            if (MainGrid != null)
                MainGrid.IsVisible = false;

            this.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnClose(object? sender, PointerPressedEventArgs e) => Hide(HiddenState.Close);

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Hide(HiddenState.Close);
        }
    }
}