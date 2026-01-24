using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Reactive;
using System.Threading.Tasks;
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
                await WavSounds.OK();

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
                
                if (ButtonList.SelectedIndex > 0)
                    ButtonList.SelectedIndex--;
            }
            else if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
            {
                e.Handled = true;
                await WavSounds.Click();

                if (ButtonList.SelectedIndex < ViewModel.ButtonItems.Count - 1)
                    ButtonList.SelectedIndex++;
            }
            else if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;

                if (ButtonList.SelectedIndex >= 0 && ButtonList.SelectedIndex < ViewModel.ButtonItems.Count)
                {
                    await WavSounds.OK();

                    var item = ViewModel.ButtonItems[ButtonList.SelectedIndex];
                    ViewModel.StartBinding(item.ButtonName);
                    FocusView();
                }
            }
        }

        private bool _isTapProcessing = false;

        private async void OnButtonItemTapped(object? sender, TappedEventArgs e)
        {
            e.Handled = true;

            if (_isTapProcessing) return;

            _isTapProcessing = true;

            try
            {
                if (ViewModel == null) return;

                if (sender is Border border && border.Tag is string buttonName)
                {
                    await WavSounds.OK();

                    ViewModel.StartBinding(buttonName);
                    FocusView();
                }
            }
            finally
            {
                await Task.Delay(500);
                _isTapProcessing = false;
            }
        }

        private void OnOverlayTapped(object? sender, TappedEventArgs e) => e.Handled = true;

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
            await WavSounds.Cancel();

            ViewModel?.GoBack();

            if (e != null) e.Handled = true;
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