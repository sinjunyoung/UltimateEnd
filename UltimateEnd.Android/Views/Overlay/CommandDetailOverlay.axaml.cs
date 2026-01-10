using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Linq;
using UltimateEnd.Android.Models;
using UltimateEnd.Android.ViewModels;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Android.Views.Overlays
{
    public partial class CommandDetailOverlay : BaseOverlay
    {
        public override bool Visible => MainGrid.IsVisible;

        private TextBox? _currentTargetTextBox;
        private IntentExtra? _currentTargetExtra;

        public event EventHandler? AppPickerRequested;
        public event EventHandler? TemplateVariablePickerRequested;
        public event EventHandler? ActionPickerRequested;
        public event EventHandler? CategoryPickerRequested;
        public event EventHandler<IntentExtra>? ExtraTypePickerRequested;
        public event EventHandler? PlatformPickerRequested;

        public CommandDetailOverlay() => InitializeComponent();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible)
            {
                base.OnKeyDown(e);
                return;
            }

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                if (e.Source is TextBox || e.Source is ComboBox)
                    return;

                e.Handled = true;
                Hide(HiddenState.Cancel);
                return;
            }

            base.OnKeyDown(e);
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();
        }

        public override void Hide(HiddenState state)
        {
            MainGrid.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnClose(object? sender, PointerPressedEventArgs e)
        {
            Hide(HiddenState.Close);
            e.Handled = true;
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Hide(HiddenState.Cancel);
        }

        private void OnPanelClick(object? sender, PointerPressedEventArgs e) => e.Handled = true;

        private void OnShowAppPickerClicked(object? sender, RoutedEventArgs e)
        {
            AppPickerRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnShowActionPickerClicked(object? sender, RoutedEventArgs e)
        {
            ActionPickerRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnShowCategoryPickerClicked(object? sender, RoutedEventArgs e)
        {
            CategoryPickerRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnShowExtraTypePickerClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is IntentExtra extra)
                ExtraTypePickerRequested?.Invoke(this, extra);

            e.Handled = true;
        }

        private void OnShowPlatformPickerClicked(object? sender, RoutedEventArgs e)
        {
            PlatformPickerRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnAddExtraClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is EmulatorSettingViewModel vm)
                vm.AddExtraCommand.Execute(null);

            e.Handled = true;
        }

        private void OnRemoveExtraClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is IntentExtra extra)
                if (DataContext is EmulatorSettingViewModel vm)
                    vm.RemoveExtraCommand.Execute(extra);

            e.Handled = true;
        }

        private void OnRemovePlatformClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PlatformTag platform)
                if (DataContext is EmulatorSettingViewModel vm)
                    vm.RemovePlatformCommand.Execute(platform);

            e.Handled = true;
        }

        private void OnInsertVariableToDataUri(object? sender, RoutedEventArgs e)
        {
            _currentTargetTextBox = this.FindControl<TextBox>("DataUriTextBox");
            _currentTargetExtra = null;

            TemplateVariablePickerRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnInsertVariableToExtra(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is IntentExtra extra)
            {
                var parent = button.Parent;

                while (parent != null && parent is not Grid)
                    parent = parent.Parent;

                if (parent is Grid grid)
                {
                    var textBoxes = grid.GetVisualDescendants().OfType<TextBox>().ToList();
                    if (textBoxes.Count > 0)
                    {
                        _currentTargetTextBox = textBoxes[0];
                        _currentTargetExtra = extra;

                        TemplateVariablePickerRequested?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            e.Handled = true;
        }

        public void InsertTemplateVariable(string variable)
        {
            if (_currentTargetTextBox == null) return;

            int caretIndex = _currentTargetTextBox.CaretIndex;
            string currentText = _currentTargetTextBox.Text ?? string.Empty;

            string newText = currentText.Insert(caretIndex, variable);

            if (_currentTargetExtra != null)
                _currentTargetExtra.Value = newText;
            else
                if (DataContext is EmulatorSettingViewModel vm)
                vm.DataUri = newText;

            _currentTargetTextBox.Text = newText;

            _currentTargetTextBox.CaretIndex = caretIndex + variable.Length;
            _currentTargetTextBox.Focus();

            _currentTargetTextBox = null;
            _currentTargetExtra = null;
        }
    }
}