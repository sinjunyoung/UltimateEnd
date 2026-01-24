using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Utils;

namespace UltimateEnd.Controls
{
    public class TouchKeyboard : UserControl
    {
        private readonly Grid _keyboardGrid;
        private TextBox? _targetTextBox;
        private KeyboardMode _currentMode = KeyboardMode.Korean;
        private readonly HangulAutomata _automata = new();
        private bool _isTouchDevice = false;
        private bool _hasDetectedInput = false;
        private readonly List<Border> _buttons = [];

        private bool _isShiftActive = false;
        private bool _isCapsLockActive = false;
        private Border? _capsLockButton;
        private Border? _shiftButton;

        private readonly Dictionary<string, string> _doubleConsonantMap = new()
        {
            { "ㅂ", "ㅃ" }, { "ㅈ", "ㅉ" }, { "ㄷ", "ㄸ" },
            { "ㄱ", "ㄲ" }, { "ㅅ", "ㅆ" }
        };

        private readonly Dictionary<string, string> _doubleVowelMap = new()
        {
            { "ㅑ", "ㅒ" }, { "ㅕ", "ㅖ" }
        };

        #region Responsive Properties

        public static readonly StyledProperty<double> KeyboardHeightProperty = AvaloniaProperty.Register<TouchKeyboard, double>(nameof(KeyboardHeight), 280);
        public static readonly StyledProperty<double> ButtonWidthProperty = AvaloniaProperty.Register<TouchKeyboard, double>(nameof(ButtonWidth), 50);
        public static readonly StyledProperty<double> ButtonHeightProperty = AvaloniaProperty.Register<TouchKeyboard, double>(nameof(ButtonHeight), 50);
        public static readonly StyledProperty<double> ButtonFontSizeProperty = AvaloniaProperty.Register<TouchKeyboard, double>(nameof(ButtonFontSize), 16);
        public static readonly StyledProperty<double> ButtonSpacingProperty = AvaloniaProperty.Register<TouchKeyboard, double>(nameof(ButtonSpacing), 4);
        public static readonly StyledProperty<Thickness> KeyboardPaddingProperty = AvaloniaProperty.Register<TouchKeyboard, Thickness>(nameof(KeyboardPadding), new Thickness(8));
        public static readonly StyledProperty<CornerRadius> ButtonCornerRadiusProperty = AvaloniaProperty.Register<TouchKeyboard, CornerRadius>(nameof(ButtonCornerRadius), new CornerRadius(6));

        public double KeyboardHeight
        {
            get => GetValue(KeyboardHeightProperty);
            set => SetValue(KeyboardHeightProperty, value);
        }

        public double ButtonWidth
        {
            get => GetValue(ButtonWidthProperty);
            set => SetValue(ButtonWidthProperty, value);
        }

        public double ButtonHeight
        {
            get => GetValue(ButtonHeightProperty);
            set => SetValue(ButtonHeightProperty, value);
        }

        public double ButtonFontSize
        {
            get => GetValue(ButtonFontSizeProperty);
            set => SetValue(ButtonFontSizeProperty, value);
        }

        public double ButtonSpacing
        {
            get => GetValue(ButtonSpacingProperty);
            set => SetValue(ButtonSpacingProperty, value);
        }

        public Thickness KeyboardPadding
        {
            get => GetValue(KeyboardPaddingProperty);
            set => SetValue(KeyboardPaddingProperty, value);
        }

        public CornerRadius ButtonCornerRadius
        {
            get => GetValue(ButtonCornerRadiusProperty);
            set => SetValue(ButtonCornerRadiusProperty, value);
        }

        #endregion

        #region Color Helpers

        private static IBrush GetBackgroundSecondary() => Application.Current?.FindResource("Background.Primary") as IBrush ?? new SolidColorBrush(Color.Parse("#22283A"));

        private static IBrush GetBackgroundInput() => Application.Current?.FindResource("Background.Card") as IBrush ?? new SolidColorBrush(Color.Parse("#2C3347"));

        private static IBrush GetBackgroundHover() => Application.Current?.FindResource("Background.Hover") as IBrush ?? new SolidColorBrush(Color.Parse("#313847"));

        private static IBrush GetBackgroundActive() => Application.Current?.FindResource("Accent.Primary") as IBrush ?? new SolidColorBrush(Color.Parse("#4A9EFF"));

        private static IBrush GetTextPrimary() => Application.Current?.FindResource("Text.Primary") as IBrush ?? Brushes.White;

        #endregion

        #region Button State Class

        private class ButtonState
        {
            public string Text { get; set; } = string.Empty;
            public Action? Action { get; set; }
            public IBrush NormalBg { get; set; } = Brushes.Transparent;
            public IBrush HoverBg { get; set; } = Brushes.Transparent;
            public IBrush ActiveBg { get; set; } = Brushes.Transparent;
            public bool IsActive { get; set; }
            public bool IsToggle { get; set; }
        }

        #endregion

        public TouchKeyboard()
        {
            _keyboardGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
                RowSpacing = ButtonSpacing,
                Focusable = false
            };
            UpdateKeyboardLayout();
            Content = _keyboardGrid;
            IsVisible = false;
            Background = GetBackgroundSecondary();

            Focusable = false;
            IsHitTestVisible = true;

            AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);

            AttachedToVisualTree += OnAttachedToVisualTree;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (OperatingSystem.IsAndroid()) _isTouchDevice = false;

            if (this.GetVisualRoot() is Interactive root)
            {
                root.AddHandler(GotFocusEvent, OnAnyControlGotFocus, RoutingStrategies.Bubble);
                root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel);
            }
        }

        private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!_hasDetectedInput && !OperatingSystem.IsAndroid())
            {
                _hasDetectedInput = true;
                var pointerType = e.GetCurrentPoint(null).Pointer.Type;
                _isTouchDevice = pointerType == PointerType.Touch || pointerType == PointerType.Pen;
            }
        }

        private void OnAnyControlGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (OperatingSystem.IsAndroid()) return;
            if (!_isTouchDevice && _hasDetectedInput) return;

            if (e.Source is TextBox textBox)
            {
                if(textBox.IsReadOnly) return;

                _targetTextBox = textBox;
                Background = GetBackgroundSecondary();
                UpdateKeyboardLayout();

                if (!IsVisible)
                {
                    IsVisible = true;
                    KeyboardEventBus.NotifyKeyboardVisibility(true);
                }
            }
            else if (IsVisible)
            {
                IsVisible = false;
                KeyboardEventBus.NotifyKeyboardVisibility(false);
            }
        }

        public void AttachToTextBox(TextBox textBox) => _targetTextBox = textBox;

        private void UpdateResponsiveSizes()
        {
            if (this.Parent is not Control parent) return;

            double width = parent.Bounds.Width;
            double height = parent.Bounds.Height;

            if (width <= 0 || height <= 0) return;

            KeyboardHeight = Math.Max(200, Math.Min(400, height * 0.35));
            Height = KeyboardHeight;

            ButtonWidth = Math.Max(40, Math.Min(80, KeyboardHeight * 0.18));
            ButtonHeight = Math.Max(40, Math.Min(70, KeyboardHeight * 0.18));
            ButtonFontSize = Math.Max(12, Math.Min(24, ButtonHeight * 0.32));
            ButtonSpacing = Math.Max(3, Math.Min(8, width * 0.003));

            double padding = Math.Max(6, Math.Min(12, KeyboardHeight * 0.03));
            KeyboardPadding = new Thickness(padding);
            Padding = KeyboardPadding;

            ButtonCornerRadius = new CornerRadius(Math.Max(4, Math.Min(8, ButtonWidth * 0.1)));

            _keyboardGrid.RowSpacing = ButtonSpacing;

            UpdateKeyboardLayout();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty) UpdateResponsiveSizes();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            AttachedToVisualTree -= OnAttachedToVisualTree;

            if (this.GetVisualRoot() is Interactive root)
            {
                root.RemoveHandler(GotFocusEvent, OnAnyControlGotFocus);
                root.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
            }

            ClearAllButtons();
        }

        private void ClearAllButtons()
        {
            foreach (var button in _buttons)
            {
                button.PointerEntered -= OnButtonPointerEntered;
                button.PointerExited -= OnButtonPointerExited;
                button.PointerReleased -= OnButtonPointerReleased;
                button.Tag = null;
            }
            _buttons.Clear();
        }

        private void UpdateKeyboardLayout()
        {
            ClearAllButtons();
            _keyboardGrid.Children.Clear();
            _keyboardGrid.RowSpacing = ButtonSpacing;
            _keyboardGrid.Focusable = false;
            _capsLockButton = null;
            _shiftButton = null;

            if (_currentMode == KeyboardMode.Korean) CreateKoreanLayout();
            else if (_currentMode == KeyboardMode.English) CreateEnglishLayout();
            else CreateNumberLayout();
        }

        private string GetDisplayChar(string key)
        {
            if (_currentMode == KeyboardMode.Korean)
            {   
                if (_isShiftActive || _isCapsLockActive)
                {
                    if (_doubleConsonantMap.TryGetValue(key, out var doubleConsonant)) return doubleConsonant;
                    if (_doubleVowelMap.TryGetValue(key, out var doubleVowel)) return doubleVowel;
                }
                return key;
            }
            else if (_currentMode == KeyboardMode.English)
            {
                if (_isShiftActive || _isCapsLockActive) return key.ToUpper();

                return key;
            }

            return key;
        }

        private void CreateKoreanLayout()
        {
            string[][] rows = [
                [ "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" ],
                [ "ㅂ", "ㅈ", "ㄷ", "ㄱ", "ㅅ", "ㅛ", "ㅕ", "ㅑ", "ㅐ", "ㅔ" ],
                [ "ㅁ", "ㄴ", "ㅇ", "ㄹ", "ㅎ", "ㅗ", "ㅓ", "ㅏ", "ㅣ" ],
                [ "ㅋ", "ㅌ", "ㅊ", "ㅍ", "ㅠ", "ㅜ", "ㅡ" ]];

            AddKeyRows(rows);
            AddBottomRowKorean();
        }

        private void CreateEnglishLayout()
        {
            string[][] rows = [
                ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"],
                ["q", "w", "e", "r", "t", "y", "u", "i", "o", "p"],
                ["a", "s", "d", "f", "g", "h", "j", "k", "l"],
                ["z", "x", "c", "v", "b", "n", "m"]];

            AddKeyRows(rows);
            AddBottomRowEnglish();
        }

        private void CreateNumberLayout()
        {
            string[][] rows = [
                ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0"],
                ["!", "@", "#", "$", "%", "^", "&", "*", "(", ")"],
                ["-", "_", "=", "+", "[", "]", "{", "}", "\\", "|"],
                [";", ":", "'", "\"", ",", ".", "<", ">", "/", "?"]];

            AddKeyRows(rows);

            var lastRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = ButtonSpacing,
                Focusable = false
            };

            lastRow.Children.Add(CreateButton("ABC", ButtonWidth * 1.4, () => SwitchMode(KeyboardMode.English)));
            lastRow.Children.Add(CreateButton("Tab", ButtonWidth * 1.2, () => InsertChar("\t")));
            lastRow.Children.Add(CreateButton("Space", ButtonWidth * 4, () => InsertChar(" ")));
            lastRow.Children.Add(CreateButton("⌫", ButtonWidth * 1.2, Backspace));
            lastRow.Children.Add(CreateButton("Enter", ButtonWidth * 1.4, () => SendKey(Key.Enter)));
            Grid.SetRow(lastRow, 4);
            _keyboardGrid.Children.Add(lastRow);
        }

        private void AddKeyRows(string[][] rows)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var stack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = ButtonSpacing,
                    Focusable = false
                };

                foreach (var key in rows[i]) stack.Children.Add(CreateButton(key, ButtonWidth));

                Grid.SetRow(stack, i);
                _keyboardGrid.Children.Add(stack);
            }
        }

        private void AddBottomRowKorean()
        {
            var lastRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = ButtonSpacing,
                Focusable = false
            };

            lastRow.Children.Add(CreateButton("한/영", ButtonWidth * 1.4, () => SwitchMode(KeyboardMode.English)));

            _capsLockButton = CreateToggleButton("Caps", ButtonWidth * 1.3, ToggleCapsLock);
            lastRow.Children.Add(_capsLockButton);

            _shiftButton = CreateToggleButton("Shift", ButtonWidth * 1.3, ActivateShift);
            lastRow.Children.Add(_shiftButton);

            lastRow.Children.Add(CreateButton("123", ButtonWidth * 1.2, () => SwitchMode(KeyboardMode.Numbers)));
            lastRow.Children.Add(CreateButton("Tab", ButtonWidth * 1.2, () => InsertChar("\t")));
            lastRow.Children.Add(CreateButton("Space", ButtonWidth * 3, () => InsertChar(" ")));
            lastRow.Children.Add(CreateButton("⌫", ButtonWidth * 1.2, Backspace));
            lastRow.Children.Add(CreateButton("Enter", ButtonWidth * 1.4, () => SendKey(Key.Enter)));

            Grid.SetRow(lastRow, 4);
            _keyboardGrid.Children.Add(lastRow);
        }

        private void AddBottomRowEnglish()
        {
            var lastRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = ButtonSpacing,
                Focusable = false
            };

            lastRow.Children.Add(CreateButton("한/영", ButtonWidth * 1.4, () => SwitchMode(KeyboardMode.Korean)));

            _capsLockButton = CreateToggleButton("Caps", ButtonWidth * 1.3, ToggleCapsLock);
            lastRow.Children.Add(_capsLockButton);

            _shiftButton = CreateToggleButton("Shift", ButtonWidth * 1.3, ActivateShift);
            lastRow.Children.Add(_shiftButton);

            lastRow.Children.Add(CreateButton("123", ButtonWidth * 1.2, () => SwitchMode(KeyboardMode.Numbers)));
            lastRow.Children.Add(CreateButton("Tab", ButtonWidth * 1.2, () => InsertChar("\t")));
            lastRow.Children.Add(CreateButton("Space", ButtonWidth * 3, () => InsertChar(" ")));
            lastRow.Children.Add(CreateButton("⌫", ButtonWidth * 1.2, Backspace));
            lastRow.Children.Add(CreateButton("Enter", ButtonWidth * 1.4, () => SendKey(Key.Enter)));

            Grid.SetRow(lastRow, 4);
            _keyboardGrid.Children.Add(lastRow);
        }

        private Border CreateButton(string text, double width, Action? action = null)
        {
            var state = new ButtonState
            {
                Text = text,
                Action = action ?? (() => InsertChar(text)),
                NormalBg = GetBackgroundInput(),
                HoverBg = GetBackgroundHover(),
                ActiveBg = GetBackgroundActive(),
                IsActive = false,
                IsToggle = false
            };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = GetTextPrimary(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = ButtonFontSize,
                Focusable = false,
                IsHitTestVisible = false
            };

            var border = new Border
            {
                Width = width,
                Height = ButtonHeight,
                Background = state.NormalBg,
                CornerRadius = ButtonCornerRadius,
                Focusable = false,
                Child = textBlock,
                Tag = state
            };

            border.PointerEntered += OnButtonPointerEntered;
            border.PointerExited += OnButtonPointerExited;
            border.PointerReleased += OnButtonPointerReleased;

            _buttons.Add(border);

            return border;
        }

        private Border CreateToggleButton(string text, double width, Action action)
        {
            var state = new ButtonState
            {
                Text = text,
                Action = action,
                NormalBg = GetBackgroundInput(),
                HoverBg = GetBackgroundHover(),
                ActiveBg = GetBackgroundActive(),
                IsActive = false,
                IsToggle = true
            };

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = GetTextPrimary(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = ButtonFontSize,
                Focusable = false,
                IsHitTestVisible = false
            };

            var border = new Border
            {
                Width = width,
                Height = ButtonHeight,
                Background = state.NormalBg,
                CornerRadius = ButtonCornerRadius,
                Focusable = false,
                Child = textBlock,
                Tag = state
            };

            border.PointerEntered += OnButtonPointerEntered;
            border.PointerExited += OnButtonPointerExited;
            border.PointerReleased += OnButtonPointerReleased;

            _buttons.Add(border);

            return border;
        }

        private void OnButtonPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Border border && border.Tag is ButtonState state)
            {
                if (!state.IsActive)
                    border.Background = state.HoverBg;
            }
        }

        private void OnButtonPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is Border border && border.Tag is ButtonState state)
                border.Background = state.IsActive ? state.ActiveBg : state.NormalBg;
        }

        private async void OnButtonPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is Border border && border.Tag is ButtonState state)
            {
                await WavSounds.Keyboard();
                state.Action?.Invoke();
            }
        }

        private void ToggleCapsLock()
        {
            _isCapsLockActive = !_isCapsLockActive;
            UpdateToggleButtonState(_capsLockButton, _isCapsLockActive);
            UpdateAllButtonTexts();
        }

        private void ActivateShift()
        {
            _isShiftActive = !_isShiftActive;
            UpdateToggleButtonState(_shiftButton, _isShiftActive);
            UpdateAllButtonTexts();
        }

        private void UpdateAllButtonTexts()
        {
            foreach (var button in _buttons)
            {
                if (button.Tag is ButtonState state && button.Child is TextBlock textBlock)
                {
                    if (state.IsToggle) continue;

                    textBlock.Text = GetDisplayChar(state.Text);
                }
            }
        }

        private static void UpdateToggleButtonState(Border? button, bool isActive)
        {
            if (button?.Tag is not ButtonState state) return;

            state.IsActive = isActive;
            button.Background = isActive ? state.ActiveBg : state.NormalBg;
        }

        private void InsertChar(string ch)
        {
            if (_targetTextBox == null) return;

            string finalChar = GetDisplayChar(ch);

            if (_currentMode == KeyboardMode.Korean)
            {
                var result = _automata.Input(finalChar);
                ApplyHangulResult(result);
            }
            else
                SendText(finalChar);

            if (_isShiftActive)
            {
                _isShiftActive = false;
                UpdateToggleButtonState(_shiftButton, false);
                UpdateAllButtonTexts();
            }
        }

        private void ApplyHangulResult(HangulResult result)
        {
            if (_targetTextBox == null) return;

            int pos = _targetTextBox.CaretIndex;
            string text = _targetTextBox.Text ?? string.Empty;

            if (_targetTextBox.SelectionEnd > _targetTextBox.SelectionStart)
            {
                int start = _targetTextBox.SelectionStart;
                int length = _targetTextBox.SelectionEnd - _targetTextBox.SelectionStart;
                text = text.Remove(start, length);
                pos = start;
                _targetTextBox.Text = text;
                _targetTextBox.CaretIndex = pos;
            }

            if (result.Action == HangulAction.Append)
            {
                _targetTextBox.Text = text.Insert(pos, result.Char.ToString());
                _targetTextBox.CaretIndex = pos + 1;
            }
            else if (result.Action == HangulAction.Update)
            {
                if (pos > 0)
                {
                    _targetTextBox.Text = text.Remove(pos - 1, 1).Insert(pos - 1, result.Char.ToString());
                    _targetTextBox.CaretIndex = pos;
                }
            }
            else if (result.Action == HangulAction.Complete)
            {
                if (pos > 0)
                    _targetTextBox.Text = text.Remove(pos - 1, 1).Insert(pos - 1, result.Completed.ToString());

                _targetTextBox.Text = _targetTextBox.Text.Insert(pos, result.Char.ToString());
                _targetTextBox.CaretIndex = pos + 1;
            }
        }

        private void Backspace()
        {
            if (_targetTextBox == null) return;

            string text = _targetTextBox.Text ?? string.Empty;
            int start = _targetTextBox.SelectionStart;
            int length = _targetTextBox.SelectionEnd - _targetTextBox.SelectionStart;

            if (length > 0)
            {
                _targetTextBox.Text = text.Remove(start, length);
                _targetTextBox.CaretIndex = start;
                _targetTextBox.SelectionStart = start;
                _targetTextBox.SelectionEnd = start;
            }
            else if (_targetTextBox.CaretIndex > 0)
            {
                int pos = _targetTextBox.CaretIndex;
                _targetTextBox.Text = text.Remove(pos - 1, 1);
                _targetTextBox.CaretIndex = pos - 1;
            }

            _automata.Reset();
        }

        private void SendText(string text)
        {
            if (_targetTextBox == null) return;

            var textInputEvent = new TextInputEventArgs
            {
                RoutedEvent = InputElement.TextInputEvent,
                Text = text,
                Source = _targetTextBox
            };

            _targetTextBox.RaiseEvent(textInputEvent);
        }

        private void SendKey(Key key)
        {
            if (_targetTextBox == null) return;

            var keyDownEvent = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = key,
                Source = _targetTextBox
            };

            _targetTextBox.RaiseEvent(keyDownEvent);

            var keyUpEvent = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyUpEvent,
                Key = key,
                Source = _targetTextBox
            };

            _targetTextBox.RaiseEvent(keyUpEvent);
        }

        private void SwitchMode(KeyboardMode mode)
        {
            _automata.Reset();
            _currentMode = mode;
            _isShiftActive = false;
            _isCapsLockActive = false;
            UpdateKeyboardLayout();
        }
    }
}