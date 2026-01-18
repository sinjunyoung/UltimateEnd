using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Reactive;
using UltimateEnd.Android.ViewModels;
using UltimateEnd.Enums;
using UltimateEnd.Utils;

namespace UltimateEnd.Android.Views
{
    public partial class KeyBindingSettingsView : UserControl
    {
        private KeyBindingSettingsViewModel? ViewModel => DataContext as KeyBindingSettingsViewModel;

        public KeyBindingSettingsView()
        {
            InitializeComponent();
            this.KeyDown += OnKeyDown;
            this.Focusable = true;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            FocusView();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            this.Focus();
        }

        private async void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;

            if (ViewModel.IsBinding)
            {
                ViewModel.HandleKeyPress(e.Key);
                e.Handled = true;
                return;
            }

            if (InputManager.IsButtonPressed(e, GamepadButton.ButtonB))
            {
                e.Handled = true;
                ViewModel.GoBackCommand?.Execute(Unit.Default);
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadUp))
            {
                e.Handled = true;
                await WavSounds.Click();
                var listBox = this.FindControl<ListBox>("ButtonList");
                if (listBox != null && listBox.SelectedIndex > 0)
                    listBox.SelectedIndex--;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
            {
                e.Handled = true;
                await WavSounds.Click();
                var listBox = this.FindControl<ListBox>("ButtonList");
                if (listBox != null && listBox.SelectedIndex < ViewModel.ButtonItems.Count - 1)
                    listBox.SelectedIndex++;
            }
            else if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;
                var listBox = this.FindControl<ListBox>("ButtonList");
                if (listBox != null && listBox.SelectedIndex >= 0 && listBox.SelectedIndex < ViewModel.ButtonItems.Count)
                {
                    await WavSounds.OK();
                    var item = ViewModel.ButtonItems[listBox.SelectedIndex];
                    ViewModel.StartBinding(item.ButtonName);
                    FocusView();
                }
            }
        }

        private async void OnButtonItemTapped(object? sender, TappedEventArgs e)
        {
            if (ViewModel == null) return;

            if (sender is Border border && border.Tag is string buttonName)
            {
                await WavSounds.OK();
                ViewModel.StartBinding(buttonName);
                FocusView();
                e.Handled = true;
            }
        }

        private void OnOverlayTapped(object? sender, TappedEventArgs e)
        {
            e.Handled = true;
        }

        private async void OnCancelBindingClick(object? sender, RoutedEventArgs e)
        {
            await WavSounds.Cancel();

            if (ViewModel != null)
            {
                ViewModel.IsBinding = false;
                FocusView();
            }
            e.Handled = true;
        }

        private async void OnBackClick(object? sender, TappedEventArgs? e)
        {
            if (ViewModel != null)
            {
                await WavSounds.Cancel();
                ViewModel.GoBackCommand?.Execute(Unit.Default);
            }

            if (e != null)
                e.Handled = true;
        }

        private void FocusView()
        {
            Dispatcher.UIThread.Post(() =>
            {
                this.Focusable = true;
                this.Focus();
            }, DispatcherPriority.Input);
        }
    }
}