using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.ViewModels;
using UltimateEnd.Views.Helpers;

namespace UltimateEnd.Views.Managers
{
    public class CarouselManager(ItemsControl platformItemsControl, Border carouselContainer, ScrollViewer carouselScrollViewer, UserControl view)
    {
        private const double SELECTION_BORDER_THICKNESS = 3.0;
        private const double CARD_ASPECT_RATIO = 1.5;
        private const double SELECTED_SCALE = 1.0;
        private const double NORMAL_SCALE = 0.5;

        private readonly ItemsControl _platformItemsControl = platformItemsControl;
        private readonly Border _carouselContainer = carouselContainer;
        private readonly ScrollViewer _carouselScrollViewer = carouselScrollViewer;
        private readonly UserControl _view = view;

        private List<Border>? _cachedPlatformCards;
        private bool _isUpdating = false;
        private int _cachedColumnsPerRow = -1;

        public void ClearCache()
        {
            _cachedPlatformCards = null;
            _cachedColumnsPerRow = -1;
        }

        public List<Border> GetPlatformCards()
        {
            if (_cachedPlatformCards != null && _cachedPlatformCards.Count > 0)
                return _cachedPlatformCards;

            var cards = _platformItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Name == "PlatformCard")
                .ToList() ?? [];

            _cachedPlatformCards = cards;

            return cards;
        }

        public void UpdateCardStylesAndScroll(PlatformListViewModel viewModel)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            try
            {
                if (viewModel == null || viewModel.IsMenuFocused) return;
                if (_platformItemsControl == null || _carouselContainer == null || _carouselScrollViewer == null)
                    return;

                var borders = GetPlatformCards();
                if (borders.Count == 0) return;

                var (normalWidth, normalHeight, selectedWidth, selectedHeight) = CalculateCardSizes();

                UpdateCardsAndIndicators(borders, viewModel.SelectedIndex,
                    normalWidth, normalHeight, selectedWidth, selectedHeight);

                BringSelectedCardIntoView(borders, viewModel.SelectedIndex);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        public void HideCardSelection()
        {
            var borders = GetPlatformCards();
            foreach (var border in borders)
            {
                var selectionBorder = border.FindDescendantByCondition<Border>(b => b.Name == "SelectionBorder");
                if (selectionBorder != null)
                    selectionBorder.BorderThickness = new Thickness(0);
            }
        }

        private (double normalWidth, double normalHeight, double selectedWidth, double selectedHeight) CalculateCardSizes()
        {
            var bounds = _carouselContainer.Bounds;
            double width = bounds.Width;
            double height = bounds.Height;

            double selectedWidth = Math.Max(150, Math.Min(500, width * 0.11));
            double selectedHeight = selectedWidth * CARD_ASPECT_RATIO;

            double normalWidth = selectedWidth * NORMAL_SCALE;
            double normalHeight = selectedHeight * NORMAL_SCALE;

            return (normalWidth, normalHeight, selectedWidth, selectedHeight);
        }

        public int CalculateVisibleCardsCount()
        {
            var cards = GetPlatformCards();
            if (_carouselContainer == null || cards.Count == 0)
                return 6;

            var (normalWidth, normalHeight, selectedWidth, selectedHeight) = CalculateCardSizes();
            var bounds = _carouselContainer.Bounds;

            double cardTotalWidth = normalWidth + 20;
            int cardsPerRow = Math.Max(1, (int)(bounds.Width / cardTotalWidth));

            return Math.Max(6, cardsPerRow * 2);
        }

        public int CalculateColumnsPerRow()
        {
            if (_cachedColumnsPerRow > 0) return _cachedColumnsPerRow;

            var cards = GetPlatformCards();

            if (cards.Count < 2) return 1;

            var (normalWidth, normalHeight, selectedWidth, selectedHeight) = CalculateCardSizes();
            var bounds = _carouselContainer.Bounds;
            double cardTotalWidth = normalWidth + 20;
            int columns = Math.Max(1, (int)Math.Floor(bounds.Width / cardTotalWidth));

            _cachedColumnsPerRow = columns;

            return columns;
        }

        private static void UpdateCardsAndIndicators(List<Border> borders, int selectedIndex, double normalWidth, double normalHeight, double selectedWidth, double selectedHeight)
        {
            for (int i = 0; i < borders.Count; i++)
            {
                bool isSelected = i == selectedIndex;
                UpdateCard(borders[i], isSelected, normalWidth, normalHeight, selectedWidth, selectedHeight);
            }
        }

        private static void UpdateCard(Border border, bool isSelected, double normalWidth, double normalHeight, double selectedWidth, double selectedHeight)
        {
            border.Width = isSelected ? selectedWidth : normalWidth;
            border.Height = isSelected ? selectedHeight : normalHeight;

            border.Opacity = isSelected ? 0.9 : 0.4;
            border.ZIndex = isSelected ? 10 : 0;

            var selectionBorder = border.FindDescendantByCondition<Border>(b => b.Name == "SelectionBorder");

            if (selectionBorder != null) selectionBorder.BorderThickness = new Thickness(isSelected ? SELECTION_BORDER_THICKNESS : 0);

            var textBlock = border.FindDescendantOfType<TextBlock>();

            if (textBlock != null)
            {
                double cardWidth = isSelected ? selectedWidth : normalWidth;
                double fontRatio = isSelected ? 0.12 : 0.08;
                double baseFontSize = cardWidth * fontRatio;
                textBlock.FontSize = Math.Max(10, baseFontSize);
            }

            var playlistIcon = border.FindDescendantByCondition<Image>(img => img.Name == "PlaylistIcon");

            if (playlistIcon != null)
            {
                double imageRatio = isSelected ? 0.15 : 0.1;
                double cardWidth = isSelected ? selectedWidth : normalWidth;
                double imageSize = cardWidth * imageRatio;
                playlistIcon.Width = imageSize;
                playlistIcon.Height = imageSize;
            }
        }

        private static void BringSelectedCardIntoView(List<Border> cards, int selectedIndex)
        {
            if (selectedIndex >= 0 && selectedIndex < cards.Count)
            {
                var selectedCard = cards[selectedIndex];
                selectedCard.BringIntoView();
            }
        }
    }
}