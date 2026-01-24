using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using System;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;
using static SQLite.TableMapping;

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

            _isInitialized = false;
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

        private void OnScrollViewerLoaded(object? sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                GameScrollViewer.Loaded -= OnScrollViewerLoaded;

                CalculateGridLayout();

                if (ViewModel?.SelectedGame != null)
                {
                    int index = ViewModel.DisplayItems
                        .Select((item, idx) => new { item, idx })
                        .FirstOrDefault(x => x.item.IsGame && x.item.Game == ViewModel.SelectedGame)
                        ?.idx ?? -1;

                    if (index >= 0) ScrollToIndex(index);
                }

                EnsureVideoStopped();
            }
        }


        private void OnScrollViewerSizeChanged(object? sender, SizeChangedEventArgs e) => CalculateGridLayout();

        #endregion

        #region Grid Layout Calculation

        public void CalculateGridLayout()
        {
            double availableWidth = GameScrollViewer.Bounds.Width;

            if (availableWidth <= 0) return;

            int MIN_ITEM_WIDTH = ThumbnailSettings.GetMaxCoverWidth();
            const double SPACING = 5;

            int columns;

            var settings = Services.SettingsService.LoadSettings();

            if (settings.GridColumns > 2)
                columns = settings.GridColumns;
            else
                columns = Math.Max(3, (int)((availableWidth + SPACING) / (MIN_ITEM_WIDTH + SPACING)));

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

            GameItemsRepeater.InvalidateMeasure();
            GameItemsRepeater.UpdateLayout();
        }

        #endregion

        #region Abstract Methods Implementation

        protected override void ScrollToItem(GameMetadata game)
        {
            if (ViewModel?.DisplayItems == null) return;

            int index = ViewModel.DisplayItems
                .Select((item, idx) => new { item, idx })
                .FirstOrDefault(x => x.item.IsGame && x.item.Game == game)
                ?.idx ?? -1;

            if (index < 0) return;

            ScrollToIndex(index);
        }

        protected override void ScrollToIndex(int index)
        {
            if (ViewModel?.Games.Count == 0 || index < 0) return;

            double availableWidth = GameScrollViewer.Bounds.Width;

            if (availableWidth <= 0) return;

            int MIN_ITEM_WIDTH = ThumbnailSettings.GetMaxCoverWidth();
            const double SPACING = 5;

            var settings = Services.SettingsService.LoadSettings();

            int columns;

            if (settings.GridColumns > 2)
                columns = settings.GridColumns;
            else
                columns = Math.Max(3, (int)((availableWidth + SPACING) / (MIN_ITEM_WIDTH + SPACING)));

            int row = index / columns;

            double actualItemWidth = (availableWidth - (SPACING * (columns + 1))) / columns;
            double actualItemHeight = actualItemWidth * 1.4;

            double rowHeight = actualItemHeight + SPACING;
            double itemTop = row * rowHeight;
            double itemBottom = itemTop + actualItemHeight;

            double viewportTop = GameScrollViewer.Offset.Y;
            double viewportBottom = viewportTop + GameScrollViewer.Viewport.Height;

            if (itemTop >= viewportTop && itemBottom <= viewportBottom) return;

            double targetOffset = viewportTop;

            if (itemTop < viewportTop)
                targetOffset = itemTop;
            else if (itemBottom > viewportBottom)
                targetOffset = itemBottom - GameScrollViewer.Viewport.Height;

            targetOffset = Math.Max(0, Math.Min(targetOffset, GameScrollViewer.ScrollBarMaximum.Y));
            GameScrollViewer.Offset = new Vector(GameScrollViewer.Offset.X, targetOffset);
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

            if (InputManager.IsButtonPressed(e, GamepadButton.DPadUp))
            {
                if (row > 0)
                    newIndex = currentIndex - _columns;
                else
                {
                    int lastRow = (count - 1) / _columns;
                    newIndex = Math.Min(count - 1, lastRow * _columns + col);
                }
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
            {
                int nextIndex = currentIndex + _columns;
                if (nextIndex < count)
                    newIndex = nextIndex;
                else
                    newIndex = Math.Min(col, count - 1);
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
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
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
            {
                if (col < _columns - 1 && currentIndex < count - 1)
                    newIndex = currentIndex + 1;
                else
                    newIndex = row * _columns;
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.RightBumper))
            {
                int pageSize = _rows * _columns;
                newIndex = Math.Min(currentIndex + pageSize, count - 1);
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.LeftBumper))
            {
                int pageSize = _rows * _columns;
                newIndex = Math.Max(currentIndex - pageSize, 0);
                e.Handled = true;
            }
            else if (e.Key == Key.Home)
            {
                newIndex = 0;
                e.Handled = true;
            }
            else if (e.Key == Key.End)
            {
                newIndex = count - 1;
                e.Handled = true;
            }

            if (newIndex >= 0 && newIndex < count && newIndex != currentIndex)
            {
                ViewModel.SelectedItem = ViewModel.DisplayItems[newIndex];
                ViewModel.StopVideo();
                ScrollToIndex(newIndex);
            }

            Dispatcher.UIThread.Post(() => GameScrollViewer.Focus(), DispatcherPriority.Input);
        }

        #endregion
    }
}