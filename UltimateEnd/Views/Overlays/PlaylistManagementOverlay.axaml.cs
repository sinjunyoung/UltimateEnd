using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class PlaylistManagementOverlay : BaseOverlay
    {
        private readonly ObservableCollection<PlaylistItem> _playlists = [];
        private PlaylistItem? _selectedItem = null;
        private PlaylistItem? _editingItem = null;
        private PlaylistItem? _deletingItem = null;
        private bool _isEditMode = false;
        private string? _selectedCoverImagePath = null;

        public override bool Visible => this.IsVisible;

        public event EventHandler<(string playlistId, string? coverImagePath)>? PlaylistCoverChanged;
        public event EventHandler<(string playlistId, string? name)>? PlaylistsNameChanged;
        public event EventHandler<string>? PlaylistsCreated;
        public event EventHandler<string>? PlaylistsDeleted;

        public PlaylistManagementOverlay()
        {
            InitializeComponent();

            this.IsVisible = false;
            PlaylistItemsControl.ItemsSource = _playlists;

            if (NameTextBox != null) NameTextBox.KeyDown += OnNameTextBoxKeyDown;

            this.AttachedToVisualTree += (s, e) =>
            {
                if (this.IsVisible && EditOverlay?.IsVisible == true) NameTextBox?.Focus();
            };

            PlaylistManager.PlaylistsChanged += OnPlaylistsChangedEvent;
        }

        private void OnPlaylistsChangedEvent() => Dispatcher.UIThread.Post(LoadPlaylists);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (EditOverlay?.IsVisible == true || DeleteOverlay?.IsVisible == true) return;

            if (e.Key == Key.Delete && _selectedItem != null)
            {
                e.Handled = true;
                ShowDeleteConfirmation(_selectedItem);
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void MovePrevious() => MoveSelection(-1);

        protected override void MoveNext() => MoveSelection(1);

        protected override void SelectCurrent()
        {
            if (_selectedItem != null)
                ShowEditOverlay(_selectedItem);
        }

        private void MoveSelection(int direction)
        {
            if (_playlists.Count == 0) return;

            int currentIndex = _selectedItem != null ? _playlists.IndexOf(_selectedItem) : -1;
            int newIndex = currentIndex + direction;

            if (newIndex < 0) newIndex = 0;
            if (newIndex >= _playlists.Count) newIndex = _playlists.Count - 1;

            if (newIndex >= 0 && newIndex < _playlists.Count)
                UpdateSelection(_playlists[newIndex]);
        }

        private void LoadPlaylists()
        {
            var playlists = PlaylistManager.Instance.GetAllPlaylists();

            _playlists.Clear();

            foreach (var playlist in playlists)
            {
                _playlists.Add(new PlaylistItem
                {
                    Playlist = playlist,
                    IsSelected = false
                });
            }

            if (_playlists.Count > 0)
            {
                _selectedItem = _playlists[0];
                _selectedItem.IsSelected = true;
                EmptyStateText.IsVisible = false;
            }
            else
            {
                _selectedItem = null;
                EmptyStateText.IsVisible = true;
            }
        }

        private async void OnPlaylistItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is PlaylistItem tappedItem)
            {
                await WavSounds.OK();
                UpdateSelection(tappedItem);
            }
            e.Handled = true;
        }

        private void OnEditButtonClick(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is Button button && button.DataContext is PlaylistItem item) ShowEditOverlay(item);
        }

        private void OnDeleteButtonClick(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is Button button && button.DataContext is PlaylistItem item) ShowDeleteConfirmation(item);
        }

        private void UpdateSelection(PlaylistItem? newItem)
        {
            foreach (var item in _playlists) item.IsSelected = (item == newItem);

            _selectedItem = newItem;
        }

        private async void OnAddClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.OK();
            ShowCreateOverlay();
        }

        private void ShowCreateOverlay()
        {
            if (EditOverlay != null && NameTextBox != null)
            {
                _isEditMode = false;
                _editingItem = null;
                _selectedCoverImagePath = null;

                EditOverlayTitle.Text = "플레이리스트 만들기";
                EditConfirmButton.Content = "만들기";
                NameTextBox.Text = string.Empty;
                EditStatusText.Text = " ";

                CoverImageDisplay.Source = null;
                EditOverlay.IsVisible = true;

                Dispatcher.UIThread.Post(() => NameTextBox.Focus(), DispatcherPriority.Loaded);
            }
        }

        private void ShowEditOverlay(PlaylistItem item)
        {
            if (EditOverlay != null && NameTextBox != null)
            {
                _isEditMode = true;
                _editingItem = item;
                _selectedCoverImagePath = item.Playlist.CoverImagePath;

                EditOverlayTitle.Text = "플레이리스트 수정";
                EditConfirmButton.Content = "수정";
                NameTextBox.Text = item.Playlist.Name;
                EditStatusText.Text = " ";

                if (!string.IsNullOrEmpty(item.Playlist.CoverImagePath) && File.Exists(item.Playlist.CoverImagePath))
                    CoverImageDisplay.Source = new Bitmap(item.Playlist.CoverImagePath);
                else
                    CoverImageDisplay.Source = null;

                EditOverlay.IsVisible = true;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    NameTextBox.Focus();
                    NameTextBox.SelectAll();
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void HideEditOverlay()
        {
            if (EditOverlay != null)
            {
                EditOverlay.IsVisible = false;
                _editingItem = null;
                _isEditMode = false;

                if (CoverImageDisplay != null)
                {
                    var oldImage = CoverImageDisplay.Source as Bitmap;
                    CoverImageDisplay.Source = null;
                    oldImage?.Dispose();
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() => this.Focus(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnNameTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA, GamepadButton.Start))
                e.Handled = true;
            else if (InputManager.IsButtonPressed(e, GamepadButton.ButtonB))
            {
                e.Handled = true;
                HideEditOverlay();
            }
        }

        private void OnEditOverlayClose(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            HideEditOverlay();
        }

        private void OnEditOverlayBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) HideEditOverlay();
        }

        private void OnEditCancel(object? sender, RoutedEventArgs e) => HideEditOverlay();

        private void OnEditConfirm(object? sender, RoutedEventArgs e) => ConfirmEdit();

        private void ConfirmEdit()
        {
            var name = NameTextBox?.Text?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                if (EditStatusText != null) EditStatusText.Text = "이름을 입력하세요";
                return;
            }

            var isDuplicate = _playlists.Any(p => p.Playlist != _editingItem?.Playlist && p.Playlist.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (isDuplicate)
            {
                if (EditStatusText != null) EditStatusText.Text = $"'{name}' 플레이리스트는 이미 존재합니다";
                return;
            }

            string? playlistId = null;
            string? oldCoverPath = null;

            if (_isEditMode && _editingItem != null)
            {
                playlistId = _editingItem.Playlist.Id;
                oldCoverPath = _editingItem.Playlist.CoverImagePath;

                _editingItem.Playlist.Name = name;
                _editingItem.Playlist.CoverImagePath = _selectedCoverImagePath;
                PlaylistManager.Instance.UpdatePlaylist(_editingItem.Playlist);

                PlaylistsNameChanged?.Invoke(this, (playlistId, name));
            }
            else
            {
                var playlist = PlaylistManager.Instance.CreatePlaylist(name);
                playlist.CoverImagePath = _selectedCoverImagePath;
                PlaylistManager.Instance.UpdatePlaylist(playlist);
                playlistId = playlist.Id;

                PlaylistsCreated?.Invoke(this, playlistId);
            }

            if (playlistId != null && oldCoverPath != _selectedCoverImagePath)
                PlaylistCoverChanged?.Invoke(this, (playlistId, _selectedCoverImagePath)); 
            
            HideEditOverlay();
        }

        private void ShowDeleteConfirmation(PlaylistItem item)
        {
            if (DeleteOverlay != null && DeleteMessageText != null)
            {
                _deletingItem = item;
                DeleteMessageText.Text = $"'{item.Playlist.Name}' 플레이리스트를 삭제하시겠습니까?";
                DeleteOverlay.IsVisible = true;
            }
        }

        private void HideDeleteOverlay()
        {
            if (DeleteOverlay != null)
            {
                DeleteOverlay.IsVisible = false;
                _deletingItem = null;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => this.Focus(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnDeleteOverlayBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) HideDeleteOverlay();
        }

        private void OnDeleteCancel(object? sender, RoutedEventArgs e) => HideDeleteOverlay();

        private void OnDeleteConfirm(object? sender, RoutedEventArgs e)
        {
            if (_deletingItem != null)
            {
                PlaylistManager.Instance.DeletePlaylist(_deletingItem.Playlist.Id);
                string id = _deletingItem.Playlist.Id;
                HideDeleteOverlay();
                PlaylistsDeleted?.Invoke(this, id);
            }
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            LoadPlaylists();

            this.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            if (MainGrid != null) MainGrid.IsVisible = true;
        }

        public override void Hide(HiddenState state)
        {
            HideEditOverlay();
            HideDeleteOverlay();

            if (MainGrid != null)
                MainGrid.IsVisible = false;

            this.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnClose(object? sender, PointerPressedEventArgs e) => Hide(HiddenState.Close);

        private void OnCloseClicked(object? sender, RoutedEventArgs e) => Hide(HiddenState.Close);

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) Hide(HiddenState.Close);
        }

        private async void OnSelectCoverImage(object? sender, RoutedEventArgs e)
        {
            try
            {
                var path = await DialogHelper.OpenFileAsync(null, FilePickerFileTypes.ImageAll);

                if (!string.IsNullOrEmpty(path))
                {
                    var converter = PathConverterFactory.Create?.Invoke();
                    _selectedCoverImagePath = converter?.UriToFriendlyPath(path) ?? path;

                    if (CoverImageDisplay != null)
                    {
                        var oldImage = CoverImageDisplay.Source as Bitmap;
                        var realPath = converter?.FriendlyPathToRealPath(_selectedCoverImagePath) ?? _selectedCoverImagePath;

                        if (File.Exists(realPath)) CoverImageDisplay.Source = new Bitmap(realPath);

                        oldImage?.Dispose();
                    }
                }
            }
            catch
            {
                if (EditStatusText != null) EditStatusText.Text = "이미지 선택 중 오류가 발생했습니다";
            }
        }

        private void OnRemoveCoverImage(object? sender, RoutedEventArgs e)
        {
            _selectedCoverImagePath = null;

            if (CoverImageDisplay != null)
            {
                var oldImage = CoverImageDisplay.Source as Bitmap;
                CoverImageDisplay.Source = null;
                oldImage?.Dispose();
            }
        }
    }
}