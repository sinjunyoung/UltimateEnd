using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Behaviors;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Scraper;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public abstract partial class GameViewBase : UserControl
    {
        #region Constants

        private const string GridModeIconData = "M3,3 H9 A1,1 0 0 1 10,4 V9 A1,1 0 0 1 9,10 H3 A1,1 0 0 1 2,9 V4 A1,1 0 0 1 3,3 Z M15,3 H21 A1,1 0 0 1 22,4 V9 A1,1 0 0 1 21,10 H15 A1,1 0 0 1 14,9 V4 A1,1 0 0 1 15,3 Z M3,15 H9 A1,1 0 0 1 10,16 V21 A1,1 0 0 1 9,22 H3 A1,1 0 0 1 2,21 V16 A1,1 0 0 1 3,15 Z M15,15 H21 A1,1 0 0 1 22,16 V21 A1,1 0 0 1 21,22 H15 A1,1 0 0 1 14,21 V16 A1,1 0 0 1 15,15 Z";
        private const string ListModeIconData = "M3,5 H21 A1,1 0 0 1 22,6 V7 A1,1 0 0 1 21,8 H3 A1,1 0 0 1 2,7 V6 A1,1 0 0 1 3,5 Z M3,11 H21 A1,1 0 0 1 22,12 V13 A1,1 0 0 1 21,14 H3 A1,1 0 0 1 2,13 V12 A1,1 0 0 1 3,11 Z M3,17 H21 A1,1 0 0 1 22,18 V19 A1,1 0 0 1 21,20 H3 A1,1 0 0 1 2,19 V18 A1,1 0 0 1 3,17 Z";

        #endregion

        #region Fields

        private bool _isLoadingMetadata = false;

        protected readonly Dictionary<string, BaseOverlay> _overlays = [];

        protected GameListViewModel? ViewModel => DataContext as GameListViewModel;

        #endregion

        #region Abstract Properties - 파생 클래스에서 구현 필요

        protected abstract ScrollViewer GameScrollViewerBase { get; }

        protected abstract ItemsRepeater GameItemsRepeaterBase { get; }

        protected abstract TextBox SearchBoxBase { get; }

        protected abstract PathIcon ViewModeIconBase { get; }

        #endregion

        #region Constructor

        protected GameViewBase() { }

        #endregion

        #region Lifecycle Overrides - 공통 + Hook 패턴

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            GameScrollViewerBase.AddHandler(InputElement.KeyDownEvent, OnGameItemsRepeaterKeyDown, RoutingStrategies.Tunnel);
            SetupSearchBox();
            InitializeOverlayEvents();

            ViewModeIconBase.Data = Geometry.Parse(ViewModel?.ViewMode == GameViewMode.Grid ? GridModeIconData : ListModeIconData);

            AutoScrapService.Instance.ScrapCompleted += OnAutoScrapCompleted;

            OnAttachedToVisualTreeCore(e);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            AutoScrapService.Instance.ScrapCompleted -= OnAutoScrapCompleted;

            OnDetachedFromVisualTreeCore(e);

            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (ViewModel != null)
                ViewModel.RequestExplicitScroll += OnRequestExplicitScroll;

            OnDataContextChangedCore(e);

            GameScrollViewerFocusLoaded();
        }

        protected async override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            
            if (ViewModel == null) return;

            if (SearchBoxBase.IsFocused) return;

            if (await HandleCommonKeyInput(e))
                return;

            OnKeyDownCore(e);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var props = e.GetCurrentPoint(this).Properties;
            if (props.IsRightButtonPressed)
            {
                ViewModel?.GoBack();
                e.Handled = true;
            }
        }
        #endregion

        #region Virtual Hook Methods - 파생 클래스에서 필요 시 재정의

        protected new virtual void OnAttachedToVisualTreeCore(VisualTreeAttachmentEventArgs e) 
        {
            if (ScreenScraperConfig.Instance.EnableAutoScrap)
                AutoScrapService.Instance.ProgressChanged += OnAutoScrapProgress;
        }

        protected new virtual void OnDetachedFromVisualTreeCore(VisualTreeAttachmentEventArgs e)
        {
            if (ScreenScraperConfig.Instance.EnableAutoScrap)
                AutoScrapService.Instance.ProgressChanged -= OnAutoScrapProgress;
        }

        protected virtual void OnDataContextChangedCore(EventArgs e) { }

        protected virtual void OnKeyDownCore(KeyEventArgs e) { }

        #endregion

        #region Abstract Methods - 파생 클래스에서 반드시 구현

        protected abstract void ScrollToItem(GameMetadata game);

        protected abstract void ScrollToIndex(int index);

        protected abstract void OnGameItemsRepeaterKeyDown(object? sender, KeyEventArgs e);

        #endregion

        #region Common Input Handling

        private async Task<bool> HandleCommonKeyInput(KeyEventArgs e)
        {
            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                if (HandleButtonBPress(e))
                    return true;
            }

            if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                if (ViewModel?.SelectedItem?.IsGame == true && ViewModel.SelectedItem.Game!.IsEditing)
                    return false;

                if (ViewModel?.SelectedItem != null)
                {
                    if (ViewModel.SelectedItem.IsFolder)
                    {
                        await WavSounds.OK();
                        ViewModel.EnterFolder(ViewModel.SelectedItem.SubFolder!);
                    }
                    else if (ViewModel.SelectedItem.IsGame)
                    {
                        await ViewModel.LaunchGameAsync(ViewModel.SelectedItem.Game!);
                    }
                }
                e.Handled = true;
                return true;
            }

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonY))
            {
                if (ViewModel?.SelectedItem?.IsGame == true)
                {
                    ViewModel.SelectedItem.Game!.IsFavorite = !ViewModel.SelectedItem.Game.IsFavorite;
                    ViewModel.RequestSave();
                    ViewModel.OnFavoritesChanged(ViewModel.SelectedItem.Game);
                }
                e.Handled = true;
                return true;
            }

            switch (e.Key)
            {
                case Key.F3:
                    Dispatcher.UIThread.Post(() =>
                    {
                        SearchBoxBase.Focus();
                        SearchBoxBase.SelectAll();
                    }, DispatcherPriority.Input);
                    e.Handled = true;
                    return true;

                case Key.Back:
                    ViewModel?.GoBack();
                    e.Handled = true;
                    return true;
            }

            return false;
        }

        protected virtual bool HandleButtonBPress(KeyEventArgs e)
        {
            ViewModel?.GoBack();
            e.Handled = true;
            return true;
        }

        #endregion

        #region Common Event Handlers

        private void OnRequestExplicitScroll(object? sender, GameMetadata game)
        {
            if (ViewModel != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    int index = ViewModel.Games.IndexOf(game);

                    if (index < 0)
                        index = ViewModel.Games.Select(g => g.RomFile).ToList().IndexOf(game.RomFile);

                    if (index >= 0)
                        ScrollToIndex(index);

                    GameScrollViewerFocusLoaded();

                }, DispatcherPriority.Background);
            }
        }

        #endregion

        #region Common UI Helpers

        private void SetupSearchBox()
        {
            SearchBoxBase.KeyDown += (s, args) =>
            {
                if (InputManager.IsButtonPressed(args.Key, GamepadButton.ButtonB))
                {
                    SearchBoxBase.Text = string.Empty;
                    ViewModel?.CommitSearch();
                    GameScrollViewerBase.Focus();
                    args.Handled = true;
                }
                else if (InputManager.IsAnyButtonPressed(args.Key, GamepadButton.ButtonA, GamepadButton.Start))
                {
                    ViewModel?.CommitSearch();
                    GameScrollViewerBase.Focus();
                    args.Handled = true;
                }
            };

            SearchBoxBase.LostFocus += (s, args) =>
            {
                ViewModel?.CommitSearch();
            };

            SearchBoxBase.GotFocus += async (s, args) =>
            {
                await Task.Delay(100);
                Dispatcher.UIThread.Post(() => SearchBoxBase.SelectAll(), DispatcherPriority.Input);
            };
        }

        protected void GameScrollViewerFocusLoaded() => Dispatcher.UIThread.Post(() => GameScrollViewerBase.Focus(), DispatcherPriority.Loaded);

        protected void ResetScrollToTop()
        {
            Dispatcher.UIThread.Post(() =>
            {
                GameScrollViewerBase.Offset = new Vector(0, 0);
            }, DispatcherPriority.Loaded);
        }

        #endregion

        #region Common Button Click Events

        protected async void OnBackClick(object? sender, PointerPressedEventArgs e)
        {
            await WavSounds.Cancel();
            ViewModel?.GoBack();
            e.Handled = true;
        }

        protected async void OnSettingsClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.OK();
            SettingsMenuOverlayBase?.SetDeletedGamesMode(ViewModel.IsShowingDeletedGames);
            SettingsMenuOverlayBase.UpdateViewMode(ViewModel.ViewMode);
            SettingsMenuOverlayBase?.Show();
        }

        protected async void OnGenreFilterClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.OK();
            GenreFilterOverlayBase?.SetGenres(ViewModel.Genres, ViewModel.SelectedGenre);
            GenreFilterOverlayBase?.Show();
        }

        #endregion

        #region Common Item Event Handlers

        protected async void OnGameItemTapped(object? sender, TappedEventArgs e)
        {
            if (GetGameFromSender(sender, out var game))
            {
                await WavSounds.OK();
                if (ViewModel != null)
                {
                    ViewModel.SelectedGame = game;
                    OnGameSelected(game);
                }
            }
        }

        protected void OnGameItemLongPress(object? sender, object item)
        {
            if (item is GameMetadata game)
                _ = ShowGameContextMenu(game);
        }

        protected async void OnGameItemDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (GetGameFromSender(sender, out var game))
                await ViewModel?.LaunchGameAsync(game);
        }

        protected async void OnMenuButtonTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is Border border && border.DataContext is FolderItem item)
                await ShowGameContextMenu(item.Game);
        }

        protected virtual void OnGameSelected(GameMetadata game) { }

        private static bool GetGameFromSender(object? sender, out GameMetadata? game)
        {
            game = null;

            if (sender is Border border && border.DataContext is GameMetadata g)
            {
                game = g;

                return true;
            }

            return false;
        }

        protected virtual GameMetadata? GetGameFromMenuButton(Border menuButton)
        {
            var grid = menuButton.Parent as Grid;
            var itemBorder = grid?.Parent as Border;

            if (itemBorder?.DataContext is GameMetadata game)
                return game;

            return null;
        }

        protected async void OnViewModeClick(object? sender, PointerPressedEventArgs e)
        {
            await WavSounds.OK();

            if (DataContext is GameListViewModel vm)
            {
                vm.ViewMode = vm.ViewMode == GameViewMode.List
                    ? GameViewMode.Grid
                    : GameViewMode.List;

                var setting = SettingsService.LoadSettings();
                setting.GameViewMode = vm.ViewMode;
                SettingsService.SaveGameListSettings(setting);

                ViewModeIconBase.Data = Geometry.Parse(ViewModel?.ViewMode == GameViewMode.Grid ? GridModeIconData : ListModeIconData);
            }

            e.Handled = true;
        }

        #endregion

        #region Overlay Helper

        protected async Task ShowGameContextMenu(GameMetadata game)
        {
            if (GameContextMenuOverlayBase == null || ViewModel == null) return;

            await WavSounds.Click();
            ViewModel.ContextMenuTargetGame = game;
            GameContextMenuOverlayBase.SelectedGame = game;
            GameContextMenuOverlayBase.Show();
        }

        protected T? GetOverlay<T>(string key) where T : class
        {
            if (_overlays.TryGetValue(key, out var overlay))
                return overlay as T;

            return null;
        }

        #endregion        

        #region Auto Scrap Status UI

        private Border? AutoScrapStatusPanel;
        private TextBlock? AutoScrapStatusText;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            AutoScrapStatusPanel = this.FindControl<Border>("AutoScrapStatusPanel");
            AutoScrapStatusText = this.FindControl<TextBlock>("AutoScrapStatusText");
        }

        private void OnAutoScrapCompleted(object? sender, AutoScrapCompletedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ViewModel != null && e.Success)
                    e.Game.RefreshMediaCache();
            });
        }

        private void OnAutoScrapProgress(object? sender, AutoScrapProgressEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (AutoScrapStatusPanel == null || AutoScrapStatusText == null)
                    return;

                bool isRunning = e.CurrentCount < e.TotalCount;
                AutoScrapStatusPanel.IsVisible = isRunning;

                if (isRunning)
                    AutoScrapStatusText.Text = $"{e.Message} ({e.CurrentCount}/{e.TotalCount})";
            });
        }

        #endregion

        protected async void OnAddAppClick(object? sender, PointerPressedEventArgs e)
        {
            await WavSounds.OK();
            await ViewModel.AddNativeAppAsync();
        }

        protected async void OnFolderMenuButtonTapped(object? sender, RoutedEventArgs e)
        {
            if (e != null) e.Handled = true;

            if (sender is Border border && border.DataContext is FolderItem item)
            {
                if (item.IsFolder)
                {
                    await WavSounds.Click();

                    var folderGame = item.Game ?? ViewModel?.Games.FirstOrDefault(g => g.SubFolder == item.SubFolder);

                    if (folderGame != null)
                    {
                        FolderContextMenuOverlayBase?.SetFolder(
                            item,
                            ViewModel!.Platform.Id,
                            folderGame.GetBasePath()
                        );
                        FolderContextMenuOverlayBase?.Show();
                    }
                }
                else if (item.IsGame)
                {
                    await ShowGameContextMenu(item.Game!);
                }
            }
        }

        protected async void OnDisplayItemTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Border border && LongPressBehavior.WasLongPressed(border))
            {
                e.Handled = true;
                return;
            }

            if (sender is Border b && b.DataContext is FolderItem item)
            {
                await WavSounds.OK();
                ViewModel?.OnItemTapped(item);
            }
        }

        protected void OnDisplayItemLongPress(object? sender, object item)
        {
            if (item is FolderItem folderItem && folderItem.IsGame)
                _ = ShowGameContextMenu(folderItem.Game);
            else if (item is FolderItem fi && fi.IsFolder)
                OnFolderMenuButtonTapped(sender, null);
        }

        protected async void OnDisplayItemDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is FolderItem item)
            {
                if (item.IsGame)
                    await ViewModel?.LaunchGameAsync(item.Game);
            }
        }
    }
}