using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Views.Overlays
{
    public partial class PlaylistSelectionOverlay : BaseOverlay
    {
        private GameMetadata? _targetGame;
        private IEnumerable<GameMetadata>? _targetGames;
        private bool _isBatchMode = false;

        public override bool Visible => OverlayMainGrid.IsVisible;

        public PlaylistSelectionOverlay() => InitializeComponent();

        public void ShowForGame(GameMetadata game)
        {
            _targetGame = game;

            if (game?.PlatformId == null) return;

            var playlists = PlaylistManager.Instance.GetAllPlaylists();

            if (playlists.Count == 0)
            {
                EmptyPlaylistText.IsVisible = true;
                PlaylistItemsRepeater.ItemsSource = null;
            }
            else
            {
                EmptyPlaylistText.IsVisible = false;

                var items = new ObservableCollection<PlaylistSelectionItem>(
                    playlists.Select(p => new PlaylistSelectionItem
                    {
                        Id = p.Id,
                        Name = p.Name,
                        IsAdded = PlaylistManager.Instance.IsGameInPlaylist(p.Id, game.PlatformId, game.RomFile)
                    })
                );

                PlaylistItemsRepeater.ItemsSource = items;
            }

            Show();
        }

        public void ShowForGames(IEnumerable<GameMetadata> games)
        {
            _targetGames = games;
            _isBatchMode = true;
            _targetGame = null;

            if (games == null || !games.Any()) return;

            var playlists = PlaylistManager.Instance.GetAllPlaylists();

            if (playlists.Count == 0)
            {
                EmptyPlaylistText.IsVisible = true;
                PlaylistItemsRepeater.ItemsSource = null;
            }
            else
            {
                EmptyPlaylistText.IsVisible = false;
                var items = new ObservableCollection<PlaylistSelectionItem>(
                    playlists.Select(p => new PlaylistSelectionItem
                    {
                        Id = p.Id,
                        Name = p.Name,
                        IsAdded = false
                    })
                );
                PlaylistItemsRepeater.ItemsSource = items;
            }

            Show();
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            OverlayMainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();
        }

        public override void Hide(HiddenState state)
        {
            OverlayMainGrid.IsVisible = false;
            _targetGame = null;
            _targetGames = null;
            _isBatchMode = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private async void OnPlaylistItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is PlaylistSelectionItem item)
            {
                if (_isBatchMode && _targetGames != null)
                {
                    foreach (var game in _targetGames)
                    {
                        if (game?.PlatformId != null)
                        {
                            if (!PlaylistManager.Instance.IsGameInPlaylist(item.Id, game.PlatformId, game.RomFile))
                                PlaylistManager.Instance.AddGameToPlaylist(item.Id, game);
                        }
                    }
                    await DialogService.Instance.ShowSuccess($"{_targetGames.Count()}개 게임을 '{item.Name}' 플레이리스트에 추가했습니다.");

                    Hide(HiddenState.Silent);
                }
                else if (_targetGame?.PlatformId != null)
                {
                    if (item.IsAdded)
                    {
                        PlaylistManager.Instance.RemoveGameFromPlaylist(item.Id, _targetGame.PlatformId, _targetGame.RomFile);
                        item.IsAdded = false;
                    }
                    else
                    {
                        PlaylistManager.Instance.AddGameToPlaylist(item.Id, _targetGame);
                        item.IsAdded = true;
                    }

                    if (PlaylistItemsRepeater.ItemsSource is ObservableCollection<PlaylistSelectionItem> currentItems)
                    {
                        PlaylistItemsRepeater.ItemsSource = null;
                        PlaylistItemsRepeater.ItemsSource = currentItems;
                    }
                }
            }
            e.Handled = true;
        }

        private void OnClose(object? sender, PointerPressedEventArgs e)
        {
            Hide(HiddenState.Close);
            e.Handled = true;
        }

        private void OnCloseButton(object? sender, RoutedEventArgs e)
        {
            Hide(HiddenState.Close);
            e.Handled = true;
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) Hide(HiddenState.Close);
        }
    }
}