using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public partial class GameListView : GameViewBase
    {
        #region Fields

        private IVideoViewInitializer? _videoViewInitializer;
        private int _visibleItemCount = 13;
        private bool _isResizing = false;
        private bool _isScrollViewerInitialized = false;
        private double _cachedHeight = 0;
        private IDisposable? _keyboardSubscription;

        #endregion

        #region Abstract Properties Implementation

        protected override ScrollViewer GameScrollViewerBase => GameScrollViewer;

        protected override ItemsRepeater GameItemsRepeaterBase => GameItemsRepeater;

        protected override TextBox SearchBoxBase => SearchBox;

        protected override PathIcon ViewModeIconBase => ViewModeIcon;

        protected override BackupListOverlay BackupListOverlayBase => BackupListOverlay;

        #endregion

        #region Constructor

        public GameListView(): base()
        {
            InitializeComponent();

            ThumbnailSettings.GameViewMode = GameViewMode.List;

            LoadSplitterPosition();
            LoadVerticalSplitterPosition();
        }

        #endregion

        #region Splitter Position Management

        private void LoadSplitterPosition()
        {   
            var settings = SettingsService.LoadSettings();            
            var topLevel = TopLevel.GetTopLevel(this);
            double windowWidth = topLevel?.Bounds.Width ?? 850;
            var savedRatio = settings.GameListSplitterPosition;

            if (MainGrid?.ColumnDefinitions.Count >= 3)
            {
                const double LEFT_MIN_WIDTH = 320;
                const double RIGHT_MIN_WIDTH = 500;
                const double SPLITTER_WIDTH = 12;

                double availableStarWidth = windowWidth - SPLITTER_WIDTH;
                double leftGridRatio;

                if (availableStarWidth < (LEFT_MIN_WIDTH + RIGHT_MIN_WIDTH))
                    leftGridRatio = 1.0;
                else
                {
                    const double MAX_RATIO_LIMIT = 3.0;
                    leftGridRatio = Math.Min(savedRatio, MAX_RATIO_LIMIT);
                }

                MainGrid.ColumnDefinitions[0].Width = new GridLength(leftGridRatio, GridUnitType.Star);
                MainGrid.ColumnDefinitions[2].Width = new GridLength(1.0, GridUnitType.Star);
            }
        }

        private void LoadVerticalSplitterPosition()
        {
            var settings = SettingsService.LoadSettings();
            var leftGrid = this.FindControl<Grid>("LeftGrid");

            if (leftGrid?.RowDefinitions.Count >= 4)
            {
                var ratio = settings.GameListVerticalSplitterPosition;

                leftGrid.RowDefinitions[1] = new RowDefinition(ratio, GridUnitType.Star);
                leftGrid.RowDefinitions[3] = new RowDefinition(1, GridUnitType.Star);
            }
        }

        private void SaveSplitterPosition()
        {
            var mainGrid = this.FindControl<Grid>("MainGrid");

            if (mainGrid?.ColumnDefinitions.Count >= 3)
            {
                double leftRatio = mainGrid.ColumnDefinitions[0].Width.Value;
                double rightRatio = mainGrid.ColumnDefinitions[2].Width.Value;
                double newRatio = leftRatio / rightRatio;
                var settings = SettingsService.LoadSettings();

                settings.GameListSplitterPosition = newRatio;
                SettingsService.SaveSettingsQuiet(settings);
            }
        }

        private void SaveVerticalSplitterPosition()
        {
            var leftGrid = this.FindControl<Grid>("LeftGrid");

            if (leftGrid?.RowDefinitions.Count >= 4)
            {
                var videoRow = leftGrid.RowDefinitions[1];
                var descRow = leftGrid.RowDefinitions[3];

                if (videoRow.ActualHeight > 0 && descRow.ActualHeight > 0)
                {
                    var ratio = videoRow.ActualHeight / descRow.ActualHeight;
                    var settings = SettingsService.LoadSettings();

                    settings.GameListVerticalSplitterPosition = ratio;
                    SettingsService.SaveSettingsQuiet(settings);
                }
            }
        }

        #endregion

        #region Hook Method Overrides

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _keyboardSubscription = KeyboardEventBus.KeyboardVisibility
                .Subscribe(isVisible =>
                {
                    if (!isVisible)
                    {
                        if (IsAnyOverlayVisible()) return;
                    }

                    VideoContainerVisible = !isVisible;
                });
        }

        private bool IsAnyOverlayVisible()
        {
            return _overlays?.Values.Any(overlay =>
            {
                if (overlay is BaseOverlay baseOverlay) return baseOverlay.Visible;

                return false;

            }) ?? false;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            _keyboardSubscription?.Dispose();
        }

        protected override void OnAttachedToVisualTreeCore(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTreeCore(e);

            VideoContainer.PointerPressed += (s, args) => args.Handled = true;
            _isScrollViewerInitialized = false;
            GameScrollViewer.Loaded += OnScrollViewerLoaded;

            InitializeVideoPlayer();
            VideoContainer.IsVisible = true;

            ViewModel.IsVideoContainerVisible = true;
            ViewModel.EnableVideoPlayback();
        }

        protected override void OnDetachedFromVisualTreeCore(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTreeCore(e);

            CleanupVideoPlayer();
        }

        protected override void OnDataContextChangedCore(EventArgs e) => InitializeVideoPlayer();

        protected override void OnKeyDownCore(KeyEventArgs e)
        {
            if (ViewModel == null) return;

            switch (e.Key)
            {
                case Key.F2:
                    if (ViewModel.SelectedItem?.IsGame == true)
                        GameListViewModel.RenameGame(ViewModel.SelectedItem.Game!);
                    e.Handled = true;
                    break;
            }
        }

        protected override bool HandleButtonBPress(KeyEventArgs e)
        {
            if (ViewModel?.SelectedGame?.IsEditingDescription == true)
            {
                ViewModel.SelectedGame.TempDescription = ViewModel.SelectedGame.Description;
                ViewModel.SelectedGame.IsEditingDescription = false;
                GameScrollViewerFocusLoaded();
                e.Handled = true;

                return true;
            }

            if (ViewModel?.IsEditingDescriptionOverlay == true)
            {
                ViewModel.IsEditingDescriptionOverlay = false;
                if (ViewModel.SelectedGame != null && ViewModel.SelectedGame.HasVideo)
                {
                    VideoContainer.IsVisible = true;
                    Dispatcher.UIThread.Post(() =>
                        ViewModel.PlayInitialVideoCommand.Execute(ViewModel.SelectedGame).Subscribe(),
                        DispatcherPriority.Background);
                }
                GameScrollViewerFocusLoaded();
                e.Handled = true;

                return true;
            }

            return base.HandleButtonBPress(e);
        }

        protected override void OnGameSelected(GameMetadata game) { }

        #endregion

        #region ScrollViewer Events

        private void OnScrollViewerLoaded(object? sender, RoutedEventArgs e)
        {
            if (_isScrollViewerInitialized) return;

            if (sender is ScrollViewer scrollViewer)
            {
                _isScrollViewerInitialized = true;
                scrollViewer.Loaded -= OnScrollViewerLoaded;

                var firstBorder = GameItemsRepeater.GetVisualDescendants()
                                                .OfType<Border>()
                                                .FirstOrDefault(b => b.Name == "GameItemBorder");

                if (firstBorder != null)
                {
                    double itemHeight = firstBorder.Bounds.Height;
                    var margin = firstBorder.Margin;
                    itemHeight += margin.Top + margin.Bottom;

                    double viewportHeight = scrollViewer.Viewport.Height;
                    _visibleItemCount = (int)(viewportHeight / itemHeight);

                    _cachedHeight = itemHeight;
                }
                if (!_isResizing && ViewModel?.SelectedGame?.HasVideo == true) Dispatcher.UIThread.Post(() => ViewModel?.PlayInitialVideoCommand.Execute(ViewModel.SelectedGame).Subscribe(), DispatcherPriority.Loaded);
            }
        }


        #endregion

        #region Abstract Methods Implementation

        protected override void ScrollToItem(GameMetadata game)
        {
            if (ViewModel == null) return;

            int index = ViewModel.DisplayItems
                .Select((item, idx) => new { item, idx })
                .FirstOrDefault(x => x.item.IsGame && x.item.Game == game)
                ?.idx ?? -1;

            if (index >= 0)
                ScrollToIndex(index);
        }

        protected override void ScrollToIndex(int index)
        {
            if (ViewModel?.DisplayItems.Count == 0 || index < 0) return;

            var firstBorder = GameItemsRepeater.GetVisualDescendants()
                                .OfType<Border>()
                                .FirstOrDefault(b => b.Name == "GameItemBorder");

            if (firstBorder != null && firstBorder.Bounds.Height > 0)
                _cachedHeight = firstBorder.Bounds.Height + firstBorder.Margin.Top + firstBorder.Margin.Bottom;

            if (_cachedHeight <= 0) return;

            double itemHeight = Math.Ceiling(_cachedHeight);
            double itemTop = index * itemHeight;
            double itemBottom = itemTop + itemHeight;

            double viewportTop = GameScrollViewer.Offset.Y;
            double viewportBottom = viewportTop + GameScrollViewer.Viewport.Height;

            const double buffer = 1.0;

            if (itemTop >= (viewportTop) && itemBottom <= (viewportBottom)) return;

            double targetY = GameScrollViewer.Offset.Y;

            if (itemTop < viewportTop)
                targetY = itemTop - buffer;
            else if (itemBottom > viewportBottom)
                targetY = itemBottom - GameScrollViewer.Viewport.Height + buffer;

            targetY = Math.Max(0, targetY);
            if (Math.Abs(GameScrollViewer.Offset.Y - targetY) > 0.5)
            {
                GameScrollViewer.Offset = new Vector(GameScrollViewer.Offset.X, targetY);
                GameItemsRepeater.TryGetElement(index);
            }
        }

        protected override async void OnGameItemsRepeaterKeyDown(object? sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;
            
            if (ViewModel.DisplayItems.Count == 0) return;
            
            if (GameRenameOverlay?.Visible == true) return;
            
            if (ViewModel.SelectedItem?.IsGame == true && ViewModel.SelectedItem.Game!.IsEditing) return;
            
            await KeySoundHelper.PlaySoundForKeyEvent(e);
            
            int count = ViewModel.DisplayItems.Count;
            int currentIndex = ViewModel.SelectedItem != null ? ViewModel.DisplayItems.IndexOf(ViewModel.SelectedItem) : 0;

            if (currentIndex < 0) currentIndex = 0;

            int newIndex = currentIndex;
            bool isCircular = false;

            if (InputManager.IsButtonPressed(e, GamepadButton.DPadUp))
            {
                if (currentIndex > 0)
                    newIndex = currentIndex - 1;
                else
                {
                    newIndex = count - 1;
                    isCircular = true;
                }
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
            {
                if (currentIndex < count - 1)
                    newIndex = currentIndex + 1;
                else
                {
                    newIndex = 0;
                    isCircular = true;
                }
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
            {
                ViewModel?.GoToPreviousPlatform();
                ResetScrollToTop();
                e.Handled = true;

                return;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
            {
                ViewModel?.GoToNextPlatform();
                ResetScrollToTop();
                e.Handled = true;

                return;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.RightBumper))
            {
                newIndex = Math.Min(currentIndex + _visibleItemCount, count - 1);
                isCircular = true;
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.LeftBumper))
            {
                newIndex = Math.Max(currentIndex - _visibleItemCount, 0);
                isCircular = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Home)
            {
                newIndex = 0;
                isCircular = true;
                e.Handled = true;
            }
            else if (e.Key == Key.End)
            {
                newIndex = count - 1;
                isCircular = true;
                e.Handled = true;
            }
            if (newIndex >= 0 && newIndex < count && newIndex != currentIndex)
            {
                ViewModel.SelectedItem = ViewModel.DisplayItems[newIndex];

                if (isCircular)
                    ScrollToIndex(newIndex);
                else if (ViewModel.SelectedItem?.IsGame == true)
                    ScrollToItem(ViewModel.SelectedItem.Game!);
            }
            Dispatcher.UIThread.Post(() => GameScrollViewer.Focus(), DispatcherPriority.Input);
        }

        #endregion

        #region Menu Button Override

        protected override GameMetadata? GetGameFromMenuButton(Border menuButton)
        {
            var panel = menuButton.Parent as Panel;
            var imageBorder = panel?.Parent as Border;
            var grid = imageBorder?.Parent as Grid;
            var itemBorder = grid?.Parent as Border;

            if (itemBorder?.DataContext is GameMetadata game) return game;

            return null;
        }

        #endregion

        #region Rename Handlers

        private void OnGameTitleLongPress(object? sender, object? dataContext)
        {
            if (dataContext is GameListViewModel vm && vm.SelectedGame != null)
            {
                vm.ContextMenuTargetGame = vm.SelectedGame;
                ShowRenameOverlay(vm.ContextMenuTargetGame);
            }
        }

        private void OnRenameTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;

                if (sender is TextBox textBox)
                {
                    var border = textBox.FindAncestorOfType<Border>();

                    if (border?.DataContext is FolderItem item && item.IsGame)
                    {
                        GameListViewModel.FinishRename(item.Game!);
                        GameScrollViewerFocusLoaded();
                    }
                }
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.ButtonB))
            {
                e.Handled = true;

                if (sender is TextBox textBox)
                {
                    var border = textBox.FindAncestorOfType<Border>();

                    if (border?.DataContext is FolderItem item && item.IsGame)
                    {
                        item.Game!.IsEditing = false;
                        GameScrollViewerFocusLoaded();
                    }
                }
            }
        }

        private void OnRenameTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var border = textBox.FindAncestorOfType<Border>();

                if (border?.DataContext is FolderItem item && item.IsGame)
                {
                    if (!item.Game!.IsEditing) return;

                    GameListViewModel.FinishRename(item.Game);
                    GameScrollViewerFocusLoaded();
                }
            }
        }

        private async void ShowRenameOverlay(GameMetadata game)
        {
            if (GameRenameOverlay == null) return;

            VideoContainer.IsVisible = false;

            await WavSounds.Click();

            GameRenameOverlay.Text = game.GetTitle();
            GameRenameOverlay.Show();
        }

        #endregion

        #region Description Handlers

        private async void OnDescriptionTextBoxLongPress(object? sender, object? dataContext)
        {
            await WavSounds.OK();

            if (ViewModel?.SelectedGame != null)
            {
                VideoContainer.IsVisible = false;
                ViewModel.IsEditingDescriptionOverlay = true;

                Dispatcher.UIThread.Post(() =>
                {
                    var overlay = this.FindControl<DescriptionEditOverlay>("DescriptionEditOverlay");
                    if (overlay != null)
                    {
                        var textBox = overlay.FindControl<TextBox>("OverlayDescriptionTextBox");
                        if (textBox != null)
                        {
                            textBox.Text = ViewModel.SelectedGame?.Description ?? string.Empty;
                            textBox.Focus();
                        }
                    }
                }, DispatcherPriority.Render);
            }
        }

        private void OnDescriptionSaveClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedGame != null)
            {
                GameListViewModel.FinishEditDescription(ViewModel?.SelectedGame);
                GameScrollViewerFocusLoaded();
            }
        }

        private void OnDescriptionCancelClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            if (ViewModel.SelectedGame != null)
            {
                ViewModel.SelectedGame.TempDescription = ViewModel.SelectedGame.Description;
                ViewModel.SelectedGame.IsEditingDescription = false;
                GameScrollViewerFocusLoaded();
            }
        }

        private void OnDescriptionTextBoxLostFocus(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel?.FocusManager == null) return;

            Dispatcher.UIThread.Post(() =>
            {
                var focusedElement = topLevel.FocusManager.GetFocusedElement() as Control;

                if (focusedElement == SaveDescriptionButton || focusedElement == CancelDescriptionButton) return;

                if (ViewModel.SelectedGame != null)
                {
                    ViewModel.SelectedGame.TempDescription = ViewModel.SelectedGame.Description;
                    ViewModel.SelectedGame.IsEditingDescription = false;
                    GameScrollViewerFocusLoaded();
                }
            }, DispatcherPriority.Input);
        }

        private void OnDescriptionOverlayCancelClick(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            ViewModel.IsEditingDescriptionOverlay = false;

            if (ViewModel.SelectedGame?.HasVideo == true)
            {
                VideoContainer.IsVisible = true;

                Dispatcher.UIThread.Post(() => ViewModel.PlayInitialVideoCommand.Execute(ViewModel.SelectedGame).Subscribe(), DispatcherPriority.Background);
            }

            GameScrollViewerFocusLoaded();
        }

        #endregion

        #region GridSplitter Events

        private void OnGridSplitterDragStarted(object? sender, VectorEventArgs e)
        {
            _isResizing = true;

            ViewModel?.StopVideo();
        }

        private void OnGridSplitterDragCompleted(object? sender, VectorEventArgs e)
        {
            _isResizing = false;
            GameScrollViewerFocusLoaded();
            SaveSplitterPosition();

            Dispatcher.UIThread.Post(async () =>
            {
                if (ViewModel?.SelectedGame?.HasVideo == true && !ViewModel.IsLaunchingGame)
                {
                    await Task.Delay(100);
                    await ViewModel.ResumeVideoAsync();
                }
            }, DispatcherPriority.Background);
        }

        private void OnVerticalGridSplitterDragCompleted(object? sender, VectorEventArgs e)
        {
            _isResizing = false;
            GameScrollViewerFocusLoaded();
            SaveVerticalSplitterPosition();

            Dispatcher.UIThread.Post(async () =>
            {
                if (ViewModel?.SelectedGame?.HasVideo == true && !ViewModel.IsLaunchingGame)
                {
                    await Task.Delay(100);
                    await ViewModel.ResumeVideoAsync();
                }
            }, DispatcherPriority.Background);
        }

        #endregion

        #region Video Player Management

        private void InitializeVideoPlayer()
        {
            _videoViewInitializer ??= VideoViewInitializerFactory.Create?.Invoke();
            _videoViewInitializer?.Initialize(VideoContainer, ViewModel?.MediaPlayer);
        }

        private void CleanupVideoPlayer() { }

        #endregion
    }
}