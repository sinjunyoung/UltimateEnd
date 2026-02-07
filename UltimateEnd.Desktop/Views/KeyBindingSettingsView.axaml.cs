using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using UltimateEnd.Desktop.ViewModels;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Utils;

namespace UltimateEnd.Desktop.Views
{
    public partial class KeyBindingSettingsView : UserControl
    {
        private KeyBindingSettingsViewModel? ViewModel => DataContext as KeyBindingSettingsViewModel;
        private List<Control> _buttonBorders = [];
        private int _selectedIndex = 0;

        private bool _isDesignMode = false;
        private Control? _draggingBorder = null;
        private Point _dragStartPoint;
        private Point _elementStartPosition;
        private TextBlock? _coordsDisplay = null;

        public KeyBindingSettingsView()
        {
            InitializeComponent();

            this.KeyDown += OnKeyDown;
            this.Focusable = true;

            CreateCoordsDisplay();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            InitializeButtons();
            FocusView();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            this.Focus();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (DataContext is KeyBindingSettingsViewModel vm) vm.UpdateGamepadConnectionStatus();
        }

        private void CreateCoordsDisplay()
        {
            _coordsDisplay = new TextBlock
            {
                FontSize = 24,
                Foreground = Brushes.Red,
                Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                Padding = new Thickness(15),
                IsVisible = false,
                TextAlignment = TextAlignment.Left
            };
        }

        private void InitializeButtons()
        {
            _buttonBorders.Clear();

            var canvas = this.FindControl<Canvas>("Canvas");

            if (canvas != null)
            {
                var borders = canvas.Children
                    .OfType<Border>()
                    .Where(b => b.Tag != null && b.Name?.StartsWith("Btn") == true)
                    .Cast<Control>();

                var grids = canvas.Children
                    .OfType<Grid>()
                    .Where(g => g.Tag != null && g.Name?.StartsWith("Btn") == true)
                    .Cast<Control>();

                _buttonBorders = [.. borders.Concat(grids).OrderBy(b => b.Name)];

                if (_coordsDisplay != null && !canvas.Children.Contains(_coordsDisplay))
                {
                    Canvas.SetLeft(_coordsDisplay, 10);
                    Canvas.SetTop(_coordsDisplay, 10);
                    canvas.Children.Add(_coordsDisplay);
                }
            }

            UpdateSelection();
        }

        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;

            if (e.Key == Key.F12)
            {
                _isDesignMode = !_isDesignMode;

                if (_coordsDisplay != null) _coordsDisplay.IsVisible = _isDesignMode;

                e.Handled = true;

                return;
            }

            if (_isDesignMode)
            {
                e.Handled = true;

                return;
            }

            if (ViewModel.IsBinding)
            {
                e.Handled = true;

                if (ViewModel.IsGamepadMode)
                {
                    if (e is GamepadKeyEventArgs gpe && gpe.IsFromGamepad && gpe.OriginalButton.HasValue)
                    {
                        int buttonIndex = GetPhysicalButtonIndex(gpe);

                        if (buttonIndex >= 0)
                        {
                            await WavSounds.OK();
                            ViewModel.HandleGamepadButtonPress(buttonIndex);
                        }
                    }
                }
                else
                {
                    if (e is GamepadKeyEventArgs gpe && gpe.IsFromGamepad && gpe.OriginalButton.HasValue)
                        return;
                    else
                    {
                        await WavSounds.OK();
                        ViewModel.HandleKeyPress(e.Key);
                    }
                }
                return;
            }

            if (InputManager.IsButtonPressed(e, GamepadButton.ButtonB))
            {
                e.Handled = true;
                await WavSounds.Cancel();
                ViewModel.GoBack();
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadUp))
            {
                e.Handled = true;
                MovePrevious();
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
            {
                e.Handled = true;
                MoveNext();
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
            {
                e.Handled = true;
                MovePrevious();
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
            {
                e.Handled = true;
                MoveNext();
            }
            else if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA))
            {
                e.Handled = true;
                SelectCurrent();
            }
        }

        private static int GetPhysicalButtonIndex(GamepadKeyEventArgs gpe) => gpe.PhysicalButtonIndex;

        private async void MovePrevious()
        {
            if (_buttonBorders.Count == 0) return;

            await WavSounds.Click();

            _selectedIndex = (_selectedIndex - 1 + _buttonBorders.Count) % _buttonBorders.Count;
            UpdateSelection();
        }

        private async void MoveNext()
        {
            if (_buttonBorders.Count == 0) return;

            await WavSounds.Click();

            _selectedIndex = (_selectedIndex + 1) % _buttonBorders.Count;
            UpdateSelection();
        }

        private async void SelectCurrent()
        {
            if (_buttonBorders.Count == 0 || _selectedIndex >= _buttonBorders.Count) return;

            await WavSounds.OK();

            var border = _buttonBorders[_selectedIndex];

            if (border.Tag is string buttonName) ViewModel?.StartBinding(buttonName);
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < _buttonBorders.Count; i++)
            {
                var control = _buttonBorders[i];

                if (i == _selectedIndex)
                {
                    if (control is Border border)
                    {
                        border.Background = this.FindResource("Background.Hover") as IBrush;
                        border.BorderThickness = new Avalonia.Thickness(3);
                    }
                    else if (control is Grid grid)
                    {
                        var path = grid.Children.OfType<Path>().FirstOrDefault();

                        if (path != null)
                        {
                            path.StrokeThickness = 3;
                            path.Fill = new SolidColorBrush(Color.FromArgb(64, 74, 158, 255));
                        }
                    }
                }
                else
                {
                    if (control is Border border)
                    {
                        border.Background = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
                        border.BorderThickness = new Avalonia.Thickness(2);
                    }
                    else if (control is Grid grid)
                    {
                        var path = grid.Children.OfType<Path>().FirstOrDefault();

                        if (path != null)
                        {
                            path.StrokeThickness = 2;
                            path.Fill = new SolidColorBrush(Color.FromArgb(21, 255, 255, 255));
                        }
                    }
                }
            }
        }

        private async void OnButtonClick(object? sender, PointerPressedEventArgs e)
        {
            if (_isDesignMode && sender is Control control)
            {
                _draggingBorder = control;
                _dragStartPoint = e.GetPosition(control.Parent as Canvas);
                _elementStartPosition = new Point(Canvas.GetLeft(control), Canvas.GetTop(control));

                control.PointerMoved += OnBorderDrag;
                control.PointerReleased += OnBorderDragEnd;

                e.Handled = true;

                return;
            }

            if (sender is Control clickedControl && clickedControl.Tag is string buttonName)
            {
                var index = _buttonBorders.IndexOf(clickedControl);

                if (index >= 0)
                {
                    _selectedIndex = index;
                    UpdateSelection();
                }

                await WavSounds.OK();

                ViewModel?.StartBinding(buttonName);

                FocusView();

                e.Handled = true;
            }
        }

        private void OnBorderDrag(object? sender, PointerEventArgs e)
        {
            if (_draggingBorder == null || !_isDesignMode) return;
            if (_draggingBorder.Parent is not Canvas canvas) return;

            var currentPoint = e.GetPosition(canvas);
            var deltaX = currentPoint.X - _dragStartPoint.X;
            var deltaY = currentPoint.Y - _dragStartPoint.Y;

            var newLeft = _elementStartPosition.X + deltaX;
            var newTop = _elementStartPosition.Y + deltaY;

            Canvas.SetLeft(_draggingBorder, newLeft);
            Canvas.SetTop(_draggingBorder, newTop);

            if (_coordsDisplay != null) _coordsDisplay.Text = $"📍 {_draggingBorder.Name}\n" + $"Canvas.Left=\"{Math.Round(newLeft)}\"\n" + $"Canvas.Top=\"{Math.Round(newTop)}\"";
        }

        private void OnBorderDragEnd(object? sender, PointerReleasedEventArgs e)
        {
            if (_draggingBorder == null) return;

            _draggingBorder.PointerMoved -= OnBorderDrag;
            _draggingBorder.PointerReleased -= OnBorderDragEnd;

            var finalLeft = Canvas.GetLeft(_draggingBorder);
            var finalTop = Canvas.GetTop(_draggingBorder);

            if (_coordsDisplay != null) _coordsDisplay.Text = $"✅ {_draggingBorder.Name}\n" + $"Canvas.Left=\"{Math.Round(finalLeft)}\"\n" + $"Canvas.Top=\"{Math.Round(finalTop)}\"\n\n" + $"👆 XAML에 복사하세요";

            _draggingBorder = null;
        }

        private async void OnBackClick(object? sender, PointerPressedEventArgs? e)
        {
            await WavSounds.Cancel();
            ViewModel?.GoBack();

            if (e != null) e.Handled = true;
        }

        public void FocusView()
        {
            Dispatcher.UIThread.Post(() => {
                this.Focusable = true;
                this.Focus();
            }, DispatcherPriority.Input);
        }
    }
}