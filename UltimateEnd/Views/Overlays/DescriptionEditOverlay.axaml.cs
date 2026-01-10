using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using UltimateEnd.Enums;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class DescriptionEditOverlay : BaseOverlay
    {
        public event EventHandler? SaveRequested;

        public override bool Visible => MainBorder.IsVisible;

        public DescriptionEditOverlay() => InitializeComponent();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible) return;

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                e.Handled = true;
                Hide(HiddenState.Cancel);
            }
        }

        public string Text
        {
            get => OverlayDescriptionTextBox?.Text ?? string.Empty;
            set
            {
                if (OverlayDescriptionTextBox != null)
                    OverlayDescriptionTextBox.Text = value;
            }
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainBorder.IsVisible = true;
            this.Focusable = true;
            this.Focus();
            OverlayDescriptionTextBox?.Focus();
        }

        public override void Hide(HiddenState state)
        {
            MainBorder.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnSave(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);

        private void OnCancel(object? sender, RoutedEventArgs e) => Hide(HiddenState.Cancel);
    }
}