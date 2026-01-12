using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Views.Managers
{
    public class PlatformDragDropManager(UserControl view, CarouselManager carouselManager)
    {
        private const double DRAG_THRESHOLD = 15.0;
        private const int LONG_PRESS_MS = 300;

        private DateTime _pressTime;

        private readonly UserControl _view = view;
        private readonly CarouselManager _carouselManager = carouselManager;

        private bool _isDragging = false;
        private bool _hasMoved = false;

        private double _originOpacity = 0;
        private int _draggedIndex = -1;
        private Point _dragStartPos;
        private Point _lastKnownPos;
        private Border? _draggedCard = null;
        private PointerPressedEventArgs? _lastPressedArgs;

        private readonly ScrollViewer? _scrollViewer = view.FindControl<ScrollViewer>("CarouselScrollViewer");
        private const double AUTO_SCROLL_SPEED = 10.0;
        private const double SCROLL_THRESHOLD = 50.0;

        public bool IsDragging => _isDragging;

        private PlatformListViewModel? ViewModel => _view.DataContext as PlatformListViewModel;

        public void Setup(Control container)
        {
            container.AddHandler(Control.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
            container.AddHandler(Control.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
            container.AddHandler(Control.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (ViewModel == null) return;

            var point = e.GetCurrentPoint(_view);
            bool isValidPress = point.Properties.IsLeftButtonPressed || point.Pointer.Type == PointerType.Touch;

            if (!isValidPress) return;

            var current = e.Source as Visual;

            while (current != null)
            {
                if (current is Border b && b.DataContext is Platform)
                {
                    if (b.Name == "PlatformCard")
                    {
                        _draggedCard = b;
                        break;
                    }
                }

                current = current.GetVisualParent();
            }

            if (_draggedCard != null)
            {
                _lastPressedArgs = e;
                var platform = (Platform)_draggedCard.DataContext;

                if (_draggedCard.Parent is Visual parent)
                {
                    _dragStartPos = e.GetCurrentPoint(parent).Position;
                    _lastKnownPos = _dragStartPos;
                }

                _draggedIndex = ViewModel.Platforms.IndexOf(platform);
                _isDragging = false;
                _hasMoved = false;
                _pressTime = DateTime.Now;

                _ = StartLongPressTimer();
            }
        }

        private async Task StartLongPressTimer()
        {
            await Task.Delay(LONG_PRESS_MS);

            if (_draggedCard == null || _hasMoved || _isDragging) return;

            Dispatcher.UIThread.Post(async () =>
            {
                if (_draggedCard != null && !_hasMoved && !_isDragging)
                {
                    _originOpacity = _draggedCard.Opacity;
                    _draggedCard.Opacity = 0.8;
                    _draggedCard.ZIndex = 9999;
                    _draggedCard.Transitions = null;

                    _lastPressedArgs?.Pointer.Capture(_view);
                    _isDragging = true;

                    await WavSounds.Success();
                }
            });
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_draggedCard == null) return;
            if (_draggedCard.Parent is not Visual parent) return;

            var point = e.GetCurrentPoint(parent);
            var currentPos = point.Position;

            _lastKnownPos = currentPos;

            if (!_isDragging)
            {
                var dx = Math.Abs(currentPos.X - _dragStartPos.X);
                var dy = Math.Abs(currentPos.Y - _dragStartPos.Y);

                if (dx > 5 || dy > 5)
                    _hasMoved = true;

                return;
            }

            var offsetX = currentPos.X - _dragStartPos.X;
            var offsetY = currentPos.Y - _dragStartPos.Y;

            _draggedCard.RenderTransform = new TranslateTransform(offsetX, offsetY);

            if (_scrollViewer != null)
            {
                var pointerInScrollViewer = e.GetCurrentPoint(_scrollViewer).Position;
                var viewportHeight = _scrollViewer.Bounds.Height;

                if (pointerInScrollViewer.Y < SCROLL_THRESHOLD)
                {
                    double speed = (SCROLL_THRESHOLD - pointerInScrollViewer.Y) / 5.0;
                    _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, Math.Max(0, _scrollViewer.Offset.Y - (AUTO_SCROLL_SPEED + speed)));
                }
                else if (pointerInScrollViewer.Y > viewportHeight - SCROLL_THRESHOLD)
                {
                    double speed = (pointerInScrollViewer.Y - (viewportHeight - SCROLL_THRESHOLD)) / 5.0;
                    double maxScroll = _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height;
                    _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, Math.Min(maxScroll, _scrollViewer.Offset.Y + (AUTO_SCROLL_SPEED + speed)));
                }
            }

            e.Handled = true;
        }

        private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_draggedCard == null || !_isDragging)
            {
                ResetDragState();
                return;
            }

            try
            {
                if (ViewModel == null) return;

                var point = e.GetCurrentPoint(_view);
                var cards = _carouselManager.GetPlatformCards();
                int targetIndex = FindClosestCardIndex(cards, point.Position);

                if (targetIndex != -1 && targetIndex != _draggedIndex)
                {
                    var item = ViewModel.Platforms[_draggedIndex];

                    ViewModel.Platforms.RemoveAt(_draggedIndex);
                    ViewModel.Platforms.Insert(targetIndex, item);

                    ViewModel.SelectedIndex = targetIndex;
                    await WavSounds.OK();

                    ViewModel.SavePlatformOrder();
                }
            }
            finally
            {
                e.Pointer.Capture(null);

                if (_draggedCard != null)
                {
                    _draggedCard.RenderTransform = null;
                    _draggedCard.Opacity = 1.0;
                    _draggedCard.ZIndex = 0;
                }

                _isDragging = false;
                _hasMoved = false;
                _draggedCard = null;

                _carouselManager.ClearCache();

                Dispatcher.UIThread.Post(() =>
                {
                    if (ViewModel != null)
                        _carouselManager.UpdateCardStylesAndScroll(ViewModel);
                }, DispatcherPriority.Background);
            }
        }

        private int FindClosestCardIndex(List<Border> cards, Point dragPosition)
        {
            int targetIndex = -1;
            double minDistance = double.MaxValue;

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == _draggedCard) continue;

                var cardPos = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height / 2), _view);

                if (cardPos.HasValue)
                {
                    double dx = dragPosition.X - cardPos.Value.X;
                    double dy = dragPosition.Y - cardPos.Value.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        targetIndex = i;
                    }
                }
            }

            return targetIndex;
        }

        private void ResetDragState()
        {
            _isDragging = false;
            _hasMoved = false;
            _draggedCard = null;
            _draggedIndex = -1;
        }
    }
}