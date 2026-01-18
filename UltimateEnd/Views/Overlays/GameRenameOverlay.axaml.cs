using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using UltimateEnd.Enums;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class GameRenameOverlay : BaseOverlay
    {
        public event EventHandler? SaveRequested;        
        public event EventHandler<KeyEventArgs>? KeyDownOccurred;

        public override bool Visible => MainGrid.IsVisible;

        public GameRenameOverlay()
        {
            InitializeComponent();
        }

        public string Text
        {
            get => RenameOverlayTextBox.Text ?? string.Empty;
            set => RenameOverlayTextBox.Text = value;
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            MainGrid.IsVisible = true;

            Dispatcher.UIThread.Post(() =>
            {
                RenameOverlayTextBox.Focus();
                RenameOverlayTextBox.SelectAll();
            }, DispatcherPriority.Input);            
        }

        public override void Hide(HiddenState state)
        {
            MainGrid.IsVisible = false;

            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnSave(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);

        private void OnCancel(object? sender, RoutedEventArgs e) => Hide(HiddenState.Close);

        private void OnRenameOverlayTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            KeyDownOccurred?.Invoke(this, e);

            if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA, GamepadButton.Start))
            {
                SaveRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.ButtonB))
            {
                Hide(HiddenState.Close);
                e.Handled = true;
            }
        }
    }
}