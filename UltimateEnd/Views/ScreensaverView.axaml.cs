using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;
using UltimateEnd.Managers;

namespace UltimateEnd.Views
{
    public partial class ScreensaverView : UserControl
    {
        private IVideoViewInitializer? _videoViewInitializer;

        private ScreensaverViewModel? ViewModel => DataContext as ScreensaverViewModel;

        #region Responsive Properties

        public static readonly StyledProperty<double> PlatformLogoHeightProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(PlatformLogoHeight), 60);
        public static readonly StyledProperty<double> GameTitleFontSizeProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(GameTitleFontSize), 42);
        public static readonly StyledProperty<double> TimeFontSizeProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(TimeFontSize), 56);
        public static readonly StyledProperty<double> InstructionFontSizeProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(InstructionFontSize), 24);
        public static readonly StyledProperty<Thickness> TopMarginProperty = AvaloniaProperty.Register<ScreensaverView, Thickness>(nameof(TopMargin), new Thickness(0, 10, 0, 0));
        public static readonly StyledProperty<Thickness> BottomMarginProperty = AvaloniaProperty.Register<ScreensaverView, Thickness>(nameof(BottomMargin), new Thickness(0, 0, 0, 20));
        public static readonly StyledProperty<double> TopViewboxHeightProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(TopViewboxHeight), 80);
        public static readonly StyledProperty<double> BottomStackSpacingProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(BottomStackSpacing), 12);
        public static readonly StyledProperty<double> TopStackSpacingProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(TopStackSpacing), 10);
        public static readonly StyledProperty<Thickness> ViewboxMarginProperty = AvaloniaProperty.Register<ScreensaverView, Thickness>(nameof(ViewboxMargin), new Thickness(10, 0));
        public static readonly StyledProperty<double> TitleGlowBlurProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(TitleGlowBlur), 25);
        public static readonly StyledProperty<double> TimeGlowBlurProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(TimeGlowBlur), 20);
        public static readonly StyledProperty<double> LogoGlowBlurProperty = AvaloniaProperty.Register<ScreensaverView, double>(nameof(LogoGlowBlur), 5);

        public double PlatformLogoHeight
        {
            get => GetValue(PlatformLogoHeightProperty);
            set => SetValue(PlatformLogoHeightProperty, value);
        }

        public double GameTitleFontSize
        {
            get => GetValue(GameTitleFontSizeProperty);
            set => SetValue(GameTitleFontSizeProperty, value);
        }

        public double TimeFontSize
        {
            get => GetValue(TimeFontSizeProperty);
            set => SetValue(TimeFontSizeProperty, value);
        }

        public double InstructionFontSize
        {
            get => GetValue(InstructionFontSizeProperty);
            set => SetValue(InstructionFontSizeProperty, value);
        }

        public Thickness TopMargin
        {
            get => GetValue(TopMarginProperty);
            set => SetValue(TopMarginProperty, value);
        }

        public Thickness BottomMargin
        {
            get => GetValue(BottomMarginProperty);
            set => SetValue(BottomMarginProperty, value);
        }

        public double TopViewboxHeight
        {
            get => GetValue(TopViewboxHeightProperty);
            set => SetValue(TopViewboxHeightProperty, value);
        }

        public double BottomStackSpacing
        {
            get => GetValue(BottomStackSpacingProperty);
            set => SetValue(BottomStackSpacingProperty, value);
        }

        public double TopStackSpacing
        {
            get => GetValue(TopStackSpacingProperty);
            set => SetValue(TopStackSpacingProperty, value);
        }

        public Thickness ViewboxMargin
        {
            get => GetValue(ViewboxMarginProperty);
            set => SetValue(ViewboxMarginProperty, value);
        }

        public double TitleGlowBlur
        {
            get => GetValue(TitleGlowBlurProperty);
            set => SetValue(TitleGlowBlurProperty, value);
        }

        public double TimeGlowBlur
        {
            get => GetValue(TimeGlowBlurProperty);
            set => SetValue(TimeGlowBlurProperty, value);
        }

        public double LogoGlowBlur
        {
            get => GetValue(LogoGlowBlurProperty);
            set => SetValue(LogoGlowBlurProperty, value);
        }

        #endregion

        public ScreensaverView()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (ViewModel != null) ViewModel.PropertyChanged += OnViewModelPropertyChanged;

            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    VideoContainer.Opacity = 0;

                    InitializeVideoPlayer();

                    await Task.Delay(100);

                    this.Focus();

                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var window = desktop.MainWindow;

                        if (window != null && window.WindowState != WindowState.FullScreen) window.WindowState = WindowState.FullScreen;
                    }

                    await Task.Delay(200);

                    if (ViewModel?.CurrentGame?.HasVideo == true)
                    {
                        await VideoPlayerManager.Instance.PlayWithDelayAsync(ViewModel.CurrentGame);
                        await Task.Delay(300);
                        UpdateVideoSize();
                        VideoContainer.Opacity = 1;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Screensaver initialization error: {ex}");
                }
            }, DispatcherPriority.Background);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (ViewModel != null) ViewModel.PropertyChanged -= OnViewModelPropertyChanged;

            _videoViewInitializer?.Cleanup(VideoContainer);
            VideoContainer.Children.Clear();

            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (ViewModel != null) ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(ScreensaverViewModel.CurrentGame))
            {
                if (ViewModel?.CurrentGame?.HasVideo == true)
                {
                    await VideoPlayerManager.Instance.PlayWithDelayAsync(ViewModel.CurrentGame);
                    await Task.Delay(500);
                    Dispatcher.UIThread.Post(() => UpdateVideoSize(), DispatcherPriority.Background);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (ViewModel == null) return;

            if (InputManager.IsAnyButtonPressed(e, GamepadButton.Start, GamepadButton.ButtonA))
            {
                ViewModel.NavigateToGameCommand.Execute().Subscribe();
                e.Handled = true;
                return;
            }

            ViewModel.ExitScreensaverCommand.Execute().Subscribe();
            e.Handled = true;
        }

        private void InitializeVideoPlayer()
        {
            _videoViewInitializer ??= VideoViewInitializerFactory.Create?.Invoke();

            VideoContainer.Children.Clear();
            _videoViewInitializer?.Initialize(VideoContainer, ScreensaverViewModel.MediaPlayer);

            UpdateVideoSize();
        }

        private void UpdateVideoSize()
        {
            double screenWidth = Bounds.Width;
            double screenHeight = Bounds.Height;

            if (screenWidth <= 0 || screenHeight <= 0) return;

            bool isScreenPortrait = screenHeight > screenWidth;

            double videoWidth = 4;
            double videoHeight = 3;
            
            int vw = VideoPlayerManager.Instance.VideoWidth;
            int vh = VideoPlayerManager.Instance.VideoHeight;

            if (vw > 0 && vh > 0)
            {
                videoWidth = vw;
                videoHeight = vh;
            }

            double videoAspectRatio = videoWidth / videoHeight;

            double targetWidth, targetHeight;

            if (isScreenPortrait)
            {
                if (videoAspectRatio <= 1.1)
                {
                    targetHeight = screenHeight * 0.75;
                    targetWidth = targetHeight * videoAspectRatio;

                    if (targetWidth > screenWidth * 0.85)
                    {
                        targetWidth = screenWidth * 0.85;
                        targetHeight = targetWidth / videoAspectRatio;
                    }
                }
                else
                {
                    targetWidth = screenWidth * 0.8;
                    targetHeight = targetWidth / videoAspectRatio;

                    if (targetHeight > screenHeight * 0.55)
                    {
                        targetHeight = screenHeight * 0.55;
                        targetWidth = targetHeight * videoAspectRatio;
                    }
                }
            }
            else
            {
                if (videoAspectRatio < 0.8)
                {
                    targetHeight = screenHeight * 0.5;
                    targetWidth = targetHeight * videoAspectRatio;
                }
                else if (videoAspectRatio > 1.2)
                {
                    targetWidth = screenWidth * 0.65;
                    targetHeight = targetWidth / videoAspectRatio;

                    if (targetHeight > screenHeight * 0.65)
                    {
                        targetHeight = screenHeight * 0.65;
                        targetWidth = targetHeight * videoAspectRatio;
                    }
                }
                else
                {
                    targetHeight = screenHeight * 0.55;
                    targetWidth = targetHeight * videoAspectRatio;
                }
            }

            foreach (var child in VideoContainer.Children)
            {
                if (child is Control control)
                {
                    control.Width = targetWidth;
                    control.Height = targetHeight;
                }
            }
        }

        private void UpdateResponsiveSizes()
        {
            if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

            double width = Bounds.Width;
            double height = Bounds.Height;

            bool isPortrait = height > width;

            if (isPortrait)
            {
                GameTitleFontSize = Math.Max(24, Math.Min(72, width * 0.08));
                TimeFontSize = Math.Max(24, Math.Min(72, width * 0.06));
                InstructionFontSize = Math.Max(16, Math.Min(40, width * 0.04));
                PlatformLogoHeight = Math.Max(20, Math.Min(70, width * 0.08));
            }
            else
            {
                GameTitleFontSize = Math.Max(24, Math.Min(72, height * 0.05));
                TimeFontSize = Math.Max(24, Math.Min(72, height * 0.03));
                InstructionFontSize = Math.Max(16, Math.Min(40, height * 0.020));
                PlatformLogoHeight = Math.Max(20, Math.Min(70, height * 0.04));
            }

            double topMarginValue = Math.Max(10, height * 0.012);
            double bottomMarginValue = Math.Max(15, height * 0.020);

            TopMargin = new Thickness(0, topMarginValue, 0, 0);
            BottomMargin = new Thickness(0, 0, 0, bottomMarginValue);

            TopViewboxHeight = Math.Max(60, Math.Min(150, height * 0.13));
            ViewboxMargin = new Thickness(Math.Max(10, width * 0.015), 0);

            TopStackSpacing = Math.Max(6, height * 0.010);
            BottomStackSpacing = Math.Max(8, height * 0.012);

            TitleGlowBlur = Math.Max(15, Math.Min(40, width * 0.012));
            TimeGlowBlur = Math.Max(12, Math.Min(35, width * 0.010));
            LogoGlowBlur = Math.Max(3, Math.Min(15, width * 0.003));
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty) UpdateResponsiveSizes();
        }
    }
}