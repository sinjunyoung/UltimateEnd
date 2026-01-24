using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Threading;

namespace UltimateEnd.Controls
{
    public class ScrollingTextBlock : UserControl
    {
        private TextBlock _textBlock;
        private Canvas _canvas;
        private bool _isInitialized = false;
        private CancellationTokenSource _animationCts;
        private double _lastContainerWidth = 0;
        private string _lastText = string.Empty;
        private double _lastFontSize = 0;
        private bool _isUpdating = false;

        public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<ScrollingTextBlock, string>(nameof(Text), string.Empty);
        public static readonly StyledProperty<double> ScrollSpeedProperty = AvaloniaProperty.Register<ScrollingTextBlock, double>(nameof(ScrollSpeed), 50.0);
        public static readonly new StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<ScrollingTextBlock, double>(nameof(FontSize), 14.0);
        public static readonly new StyledProperty<IBrush> ForegroundProperty = AvaloniaProperty.Register<ScrollingTextBlock, IBrush>(nameof(Foreground), Brushes.Black);
        public static readonly StyledProperty<double> ScrollThresholdProperty = AvaloniaProperty.Register<ScrollingTextBlock, double>(nameof(ScrollThreshold), 5.0);

        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public double ScrollSpeed
        {
            get => GetValue(ScrollSpeedProperty);
            set => SetValue(ScrollSpeedProperty, value);
        }

        public new double FontSize
        {
            get => GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public new IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public double ScrollThreshold
        {
            get => GetValue(ScrollThresholdProperty);
            set => SetValue(ScrollThresholdProperty, value);
        }

        public ScrollingTextBlock()
        {
            _canvas = new Canvas { ClipToBounds = true };

            _textBlock = new TextBlock
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                Opacity = 0,
                RenderTransform = new TranslateTransform()
            };

            _canvas.Children.Add(_textBlock);
            Content = _canvas;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty)
            {
                if (_textBlock != null)
                {
                    _textBlock.Text = Text;
                    RequestUpdateLayout();
                }
            }
            else if (change.Property == FontSizeProperty)
            {
                if (_textBlock != null)
                {
                    _textBlock.FontSize = FontSize;
                    RequestUpdateLayout();
                }
            }
            else if (change.Property == ForegroundProperty)
            {
                if (_textBlock != null) _textBlock.Foreground = Foreground;
            }
            else if (change.Property == BoundsProperty && _isInitialized) RequestUpdateLayout();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isInitialized = true;
            RequestUpdateLayout();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            StopAnimation();
        }

        private void RequestUpdateLayout()
        {
            if (_isUpdating) return;

            Dispatcher.UIThread.Post(UpdateLayoutInternal, DispatcherPriority.Render);
        }

        private void UpdateLayoutInternal()
        {
            if (_textBlock == null || _canvas == null || !_isInitialized || _isUpdating) return;

            _isUpdating = true;

            try
            {
                var containerWidth = Bounds.Width;

                if (containerWidth <= 0)
                {
                    _isUpdating = false;

                    return;
                }

                if (Math.Abs(containerWidth - _lastContainerWidth) < 1.0 && _lastContainerWidth > 0 && _lastText == Text && Math.Abs(_lastFontSize - FontSize) < 0.1)
                {
                    _textBlock.Opacity = 1;
                    _isUpdating = false;

                    return;
                }

                _lastContainerWidth = containerWidth;
                _lastText = Text;
                _lastFontSize = FontSize;

                _textBlock.FontSize = FontSize;
                _textBlock.Text = Text;

                _textBlock.InvalidateMeasure();
                _textBlock.InvalidateArrange();
                _textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                _textBlock.Arrange(new Rect(_textBlock.DesiredSize));

                var typeface = new Typeface(_textBlock.FontFamily, _textBlock.FontStyle, _textBlock.FontWeight);
                var formattedText = new FormattedText(
                    Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    FontSize,
                    Brushes.Black);

                var textWidth = formattedText.Width;

                StopAnimation();

                if (textWidth > containerWidth + ScrollThreshold)
                    StartScrollAnimation(containerWidth, textWidth);
                else
                {
                    var centerPosition = Math.Max(0, (containerWidth - textWidth) / 2);
                    Canvas.SetLeft(_textBlock, centerPosition);

                    if (_textBlock.RenderTransform is TranslateTransform transform)
                        transform.X = 0;

                    _textBlock.Opacity = 1;
                }
            }
            catch
            {
                _textBlock.Opacity = 1;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void StartScrollAnimation(double containerWidth, double textWidth)
        {
            Canvas.SetLeft(_textBlock, 0);
            _textBlock.Opacity = 1;

            var totalDistance = textWidth + containerWidth;
            var duration = TimeSpan.FromSeconds(totalDistance / ScrollSpeed);

            _animationCts = new CancellationTokenSource();

            var animation = new Animation
            {
                Duration = duration,
                IterationCount = IterationCount.Infinite,
                Easing = new LinearEasing(),
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0.0),
                        Setters = { new Setter(TranslateTransform.XProperty, containerWidth) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1.0),
                        Setters = { new Setter(TranslateTransform.XProperty, -textWidth) }
                    }
                }
            };

            _ = animation.RunAsync(_textBlock, _animationCts.Token);
        }

        private void StopAnimation()
        {
            if (_animationCts != null)
            {
                _animationCts.Cancel();
                _animationCts.Dispose();
                _animationCts = null;
            }
        }
    }
}