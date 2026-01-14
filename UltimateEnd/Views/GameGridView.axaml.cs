using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using System;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public partial class GameGridView : GameViewBase
    {
        #region Fields

        private int _columns = 3;
        private readonly int _rows = 3;
        private bool _isInitialized = false;

        #endregion

        #region Abstract Properties Implementation

        protected override ScrollViewer GameScrollViewerBase => GameScrollViewer;

        protected override TextBox SearchBoxBase => SearchBox;

        protected override ItemsRepeater GameItemsRepeaterBase => GameItemsRepeater;

        protected override PathIcon ViewModeIconBase => ViewModeIcon;

        protected override BackupListOverlay BackupListOverlayBase => BackupListOverlay;

        #endregion

        #region Constructor

        public GameGridView(): base()
        {
            InitializeComponent();

            ThumbnailSettings.GameViewMode = GameViewMode.Grid;
        }

        #endregion

        #region Hook Method Overrides

        protected override void OnAttachedToVisualTreeCore(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTreeCore(e);

            GameScrollViewer.SizeChanged += OnScrollViewerSizeChanged;
            GameScrollViewer.Loaded += OnScrollViewerLoaded;

            EnsureVideoStopped();
        }

        protected override void OnDetachedFromVisualTreeCore(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTreeCore(e);

            EnsureVideoStopped();

            GameScrollViewer.SizeChanged -= OnScrollViewerSizeChanged;
        }

        protected override void OnDataContextChangedCore(EventArgs e)
        {
            EnsureVideoStopped();
        }

        protected override void OnGameSelected(GameMetadata game)
        {
            ViewModel?.StopVideo();
        }

        #endregion

        #region Video Control

        private void EnsureVideoStopped()
        {
            if (ViewModel != null)
            {
                ViewModel.StopVideo();

                ViewModel.IsVideoContainerVisible = false;

                ViewModel.DisableVideoPlayback();
            }
        }

        #endregion

        #region ScrollViewer Events

        private void OnScrollViewerLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                GameScrollViewer.Loaded -= OnScrollViewerLoaded;

                Dispatcher.UIThread.Post(() =>
                {
                    CalculateGridLayout();

                    if (ViewModel?.SelectedGame != null)
                        ScrollToItem(ViewModel.SelectedGame);

                    EnsureVideoStopped();

                }, DispatcherPriority.Loaded);
            }
        }

        private void OnScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e) => CalculateGridLayout();

        #endregion

        #region Grid Layout Calculation

        private void CalculateGridLayout()
        {
            double availableWidth = GameScrollViewer.Bounds.Width;

            if (availableWidth <= 0)
                return;

            int MIN_ITEM_WIDTH = ThumbnailSettings.GetMaxCoverWidth();

            const double SPACING = 5;

            int columns = Math.Max(3, (int)((availableWidth + SPACING) / (MIN_ITEM_WIDTH + SPACING)));

            double actualItemWidth = (availableWidth - (SPACING * (columns + 1))) / columns;

            double actualItemHeight = actualItemWidth * 1.4;

            _columns = columns;

            var layout = new UniformGridLayout
            {
                MinColumnSpacing = SPACING,
                MinRowSpacing = SPACING,
                MinItemWidth = actualItemWidth,
                MinItemHeight = actualItemHeight,
                MaximumRowsOrColumns = columns,
                ItemsStretch = UniformGridLayoutItemsStretch.None
            };

            GameItemsRepeater.Layout = layout;
        }

        #endregion

        #region Abstract Methods Implementation

        protected override void ScrollToItem(GameMetadata game)
        {
            if (ViewModel?.Games == null) return;

            int index = ViewModel.Games.IndexOf(game);
            if (index < 0) return;

            ScrollToIndex(index);
        }

        protected override void ScrollToIndex(int index)
        {
            if (ViewModel?.Games.Count == 0) return;

            int row = index / _columns;

            double availableWidth = GameScrollViewer.Bounds.Width;
            if (availableWidth <= 0) return;

            int MIN_ITEM_WIDTH = ThumbnailSettings.GetMaxCoverWidth();
            const double SPACING = 5;

            int columns = Math.Max(3, (int)((availableWidth + SPACING) / (MIN_ITEM_WIDTH + SPACING)));
            double actualItemWidth = (availableWidth - (SPACING * (columns + 1))) / columns;
            double actualItemHeight = actualItemWidth * 1.4;

            double rowHeight = actualItemHeight + SPACING;
            double targetOffset = row * rowHeight;

            double viewportHeight = GameScrollViewer.Viewport.Height;
            double currentOffset = GameScrollViewer.Offset.Y;

            if (targetOffset < currentOffset || targetOffset + actualItemHeight > currentOffset + viewportHeight)
            {
                double centeredOffset = targetOffset - (viewportHeight / 2) + (actualItemHeight / 2);
                centeredOffset = Math.Max(0, Math.Min(centeredOffset, GameScrollViewer.ScrollBarMaximum.Y));

                GameScrollViewer.Offset = new Vector(GameScrollViewer.Offset.X, centeredOffset);
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

            int row = currentIndex / _columns;
            int col = currentIndex % _columns;
            int newIndex = currentIndex;
            bool needsScroll = false;

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadUp))
            {
                if (row > 0)
                {
                    newIndex = currentIndex - _columns;
                    needsScroll = true;
                }
                else
                {
                    int lastRow = (count - 1) / _columns;
                    newIndex = Math.Min(count - 1, lastRow * _columns + col);
                    needsScroll = true;
                }
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadDown))
            {
                int nextIndex = currentIndex + _columns;
                if (nextIndex < count)
                {
                    newIndex = nextIndex;
                    needsScroll = true;
                }
                else
                {
                    newIndex = Math.Min(col, count - 1);
                    needsScroll = true;
                }
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadLeft))
            {
                if (col > 0)
                    newIndex = currentIndex - 1;
                else
                {
                    int lastColInRow = Math.Min(_columns - 1, count - 1 - row * _columns);
                    newIndex = row * _columns + lastColInRow;
                }
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadRight))
            {
                if (col < _columns - 1 && currentIndex < count - 1)
                    newIndex = currentIndex + 1;
                else
                    newIndex = row * _columns;
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.RightBumper))
            {
                int pageSize = _rows * _columns;
                newIndex = Math.Min(currentIndex + pageSize, count - 1);
                needsScroll = true;
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.LeftBumper))
            {
                int pageSize = _rows * _columns;
                newIndex = Math.Max(currentIndex - pageSize, 0);
                needsScroll = true;
                e.Handled = true;
            }
            else if (e.Key == Key.Home)
            {
                newIndex = 0;
                needsScroll = true;
                e.Handled = true;
            }
            else if (e.Key == Key.End)
            {
                newIndex = count - 1;
                needsScroll = true;
                e.Handled = true;
            }

            if (newIndex >= 0 && newIndex < count && newIndex != currentIndex)
            {
                ViewModel.SelectedItem = ViewModel.DisplayItems[newIndex];
                ViewModel.StopVideo();

                if (needsScroll)
                    ScrollToItem(ViewModel.SelectedGame);
            }

            Dispatcher.UIThread.Post(() => GameScrollViewer.Focus(), DispatcherPriority.Input);
        }

        #endregion
    }
}