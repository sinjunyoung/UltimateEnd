using Avalonia;
using System;

namespace UltimateEnd.Views
{
    public partial class PlatformListView
    {
        #region Responsive Properties

        public static readonly StyledProperty<double> CardWidthProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(CardWidth), 180);

        public static readonly StyledProperty<double> CardHeightProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(CardHeight), 270);

        public static readonly StyledProperty<Thickness> CardMarginProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(CardMargin), new Thickness(12));

        public static readonly StyledProperty<Thickness> ViewMarginProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(ViewMargin), new Thickness(40));

        public static readonly StyledProperty<Thickness> CarouselMarginProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(CarouselMargin), new Thickness(40, 120, 40, 40));

        public static readonly StyledProperty<CornerRadius> CardCornerRadiusProperty = AvaloniaProperty.Register<PlatformListView, CornerRadius>(nameof(CardCornerRadius), new CornerRadius(12));

        public static readonly StyledProperty<double> CardFontSizeProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(CardFontSize), 15);

        public static readonly StyledProperty<Thickness> CardImagePaddingProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(CardImagePadding), new Thickness(18));

        public static readonly StyledProperty<Thickness> CardTextPaddingProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(CardTextPadding), new Thickness(12));

        public static readonly StyledProperty<double> ShadowBlurProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(ShadowBlur), 18);

        public static readonly StyledProperty<double> ShadowOffsetProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(ShadowOffset), 8);

        public static readonly StyledProperty<Thickness> SelectionBorderThicknessProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(SelectionBorderThickness), new Thickness(0));

        public static readonly StyledProperty<double> TitleFontSizeProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(TitleFontSize), 38);

        public static readonly StyledProperty<double> SubtitleFontSizeProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(SubtitleFontSize), 16);

        public static readonly StyledProperty<Thickness> SubtitleMarginProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(SubtitleMargin), new Thickness(0, 4, 0, 0));

        public static readonly StyledProperty<Thickness> HeaderMarginProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(HeaderMargin), new Thickness(40, 30, 40, 0));

        public static readonly StyledProperty<double> IconSizeProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(IconSize), 36);

        public static readonly StyledProperty<double> IconSpacingProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(IconSpacing), 16);

        public static readonly StyledProperty<double> ClockFontSizeProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(ClockFontSize), 32);

        public static readonly StyledProperty<double> DateFontSizeProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(DateFontSize), 16);

        public static readonly StyledProperty<Thickness> DateMarginProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(DateMargin), new Thickness(0, 4, 0, 0));

        public static readonly StyledProperty<Thickness> ClockMarginProperty = AvaloniaProperty.Register<PlatformListView, Thickness>(nameof(ClockMargin), new Thickness(0, 0, 40, 30));

        public static readonly StyledProperty<double> LoadingFontSizeProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(LoadingFontSize), 24);

        public static readonly StyledProperty<double> LoadingSubFontSizeProperty = AvaloniaProperty.Register<PlatformListView, double>(nameof(LoadingSubFontSize), 16);

        public double CardWidth
        {
            get => GetValue(CardWidthProperty);
            set => SetValue(CardWidthProperty, value);
        }

        public double CardHeight
        {
            get => GetValue(CardHeightProperty);
            set => SetValue(CardHeightProperty, value);
        }

        public Thickness CardMargin
        {
            get => GetValue(CardMarginProperty);
            set => SetValue(CardMarginProperty, value);
        }

        public Thickness ViewMargin
        {
            get => GetValue(ViewMarginProperty);
            set => SetValue(ViewMarginProperty, value);
        }

        public Thickness CarouselMargin
        {
            get => GetValue(CarouselMarginProperty);
            set => SetValue(CarouselMarginProperty, value);
        }

        public CornerRadius CardCornerRadius
        {
            get => GetValue(CardCornerRadiusProperty);
            set => SetValue(CardCornerRadiusProperty, value);
        }

        public double CardFontSize
        {
            get => GetValue(CardFontSizeProperty);
            set => SetValue(CardFontSizeProperty, value);
        }

        public Thickness CardImagePadding
        {
            get => GetValue(CardImagePaddingProperty);
            set => SetValue(CardImagePaddingProperty, value);
        }

        public Thickness CardTextPadding
        {
            get => GetValue(CardTextPaddingProperty);
            set => SetValue(CardTextPaddingProperty, value);
        }

        public double ShadowBlur
        {
            get => GetValue(ShadowBlurProperty);
            set => SetValue(ShadowBlurProperty, value);
        }

        public double ShadowOffset
        {
            get => GetValue(ShadowOffsetProperty);
            set => SetValue(ShadowOffsetProperty, value);
        }

        public Thickness SelectionBorderThickness
        {
            get => GetValue(SelectionBorderThicknessProperty);
            set => SetValue(SelectionBorderThicknessProperty, value);
        }

        public double TitleFontSize
        {
            get => GetValue(TitleFontSizeProperty);
            set => SetValue(TitleFontSizeProperty, value);
        }

        public double SubtitleFontSize
        {
            get => GetValue(SubtitleFontSizeProperty);
            set => SetValue(SubtitleFontSizeProperty, value);
        }

        public Thickness SubtitleMargin
        {
            get => GetValue(SubtitleMarginProperty);
            set => SetValue(SubtitleMarginProperty, value);
        }

        public Thickness HeaderMargin
        {
            get => GetValue(HeaderMarginProperty);
            set => SetValue(HeaderMarginProperty, value);
        }

        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public double IconSpacing
        {
            get => GetValue(IconSpacingProperty);
            set => SetValue(IconSpacingProperty, value);
        }

        public double ClockFontSize
        {
            get => GetValue(ClockFontSizeProperty);
            set => SetValue(ClockFontSizeProperty, value);
        }

        public double DateFontSize
        {
            get => GetValue(DateFontSizeProperty);
            set => SetValue(DateFontSizeProperty, value);
        }

        public Thickness DateMargin
        {
            get => GetValue(DateMarginProperty);
            set => SetValue(DateMarginProperty, value);
        }

        public Thickness ClockMargin
        {
            get => GetValue(ClockMarginProperty);
            set => SetValue(ClockMarginProperty, value);
        }

        public double LoadingFontSize
        {
            get => GetValue(LoadingFontSizeProperty);
            set => SetValue(LoadingFontSizeProperty, value);
        }

        public double LoadingSubFontSize
        {
            get => GetValue(LoadingSubFontSizeProperty);
            set => SetValue(LoadingSubFontSizeProperty, value);
        }

        #endregion

        #region Responsive Calculation

        private void UpdateResponsiveSizes()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

            double width = Bounds.Width;
            double height = Bounds.Height;

            TitleFontSize = Math.Max(28, Math.Min(72, height * 0.025));
            SubtitleFontSize = Math.Max(14, Math.Min(32, height * 0.012));
            CardFontSize = Math.Max(13, Math.Min(24, height * 0.011));
            ClockFontSize = Math.Max(24, Math.Min(64, height * 0.022));
            DateFontSize = Math.Max(14, Math.Min(32, height * 0.012));
            LoadingFontSize = Math.Max(20, Math.Min(48, height * 0.018));
            LoadingSubFontSize = Math.Max(14, Math.Min(32, height * 0.012));

            double viewMargin = Math.Max(20, width * 0.02);
            double sideMargin = Math.Max(20, width * 0.02);
            double topMargin = Math.Max(15, height * 0.028);

            HeaderMargin = new Thickness(sideMargin, topMargin, sideMargin, 0);
            ClockMargin = new Thickness(0, 0, sideMargin, topMargin);

            double headerContentHeight = TitleFontSize + SubtitleFontSize + (height * 0.004) + (height * 0.02);
            double carouselTopMargin = topMargin + headerContentHeight;
            CarouselMargin = new Thickness(viewMargin, carouselTopMargin, viewMargin, viewMargin);

            CardWidth = Math.Max(120, Math.Min(400, width * 0.09));
            CardHeight = CardWidth * 1.5;

            double cardMargin = Math.Max(8, width * 0.006);
            CardMargin = new Thickness(cardMargin);
            ViewMargin = new Thickness(viewMargin);

            CardCornerRadius = new CornerRadius(Math.Max(8, width * 0.006));
            double imagePadding = Math.Max(12, CardWidth * 0.1);
            CardImagePadding = new Thickness(imagePadding);
            double textPadding = Math.Max(8, CardWidth * 0.067);
            CardTextPadding = new Thickness(textPadding);

            ShadowBlur = Math.Max(12, width * 0.009);
            ShadowOffset = Math.Max(4, width * 0.004);

            SubtitleMargin = new Thickness(0, Math.Max(2, height * 0.004), 0, 0);
            DateMargin = new Thickness(0, Math.Max(2, height * 0.004), 0, 0);

            IconSize = Math.Max(24, Math.Min(72, height * 0.033));
            IconSpacing = Math.Max(12, width * 0.008);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty)
                UpdateResponsiveSizes();
        }

        #endregion
    }
}