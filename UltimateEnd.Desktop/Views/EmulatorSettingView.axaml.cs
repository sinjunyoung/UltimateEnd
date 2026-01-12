using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using UltimateEnd.Desktop.ViewModels;
using UltimateEnd.Enums;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Desktop.Views
{
    public partial class EmulatorSettingView : UserControl
    {
        private EmulatorSettingViewModel? ViewModel => DataContext as EmulatorSettingViewModel;

        public EmulatorSettingView()
        {
            InitializeComponent();
            InitializeOverlays();

            CommandListBox.KeyDown += OnCommandListKeyDown;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            FocusCommandList();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (ViewModel != null)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel != null)
                    ViewModel.SetStorageProvider(topLevel.StorageProvider);
            }
        }

        private void InitializeOverlays()
        {
            BaseOverlay[] overlays = [
                TemplateVariablePickerOverlay,
            ];

            foreach (var overlay in overlays)
            {
                overlay.Showing += async (s, e) => { await WavSounds.OK(); };

                overlay.Hidden += async (s, e) =>
                {
                    if (e.State == HiddenState.Cancel)
                        await WavSounds.Cancel();
                    else if (e.State == HiddenState.Confirm)
                        await WavSounds.OK();

                    FocusCommandList();
                };

                overlay.Click += async (s, e) => await WavSounds.Click();
            }

            TemplateVariablePickerOverlay.VariableSelected += OnTemplateVariableSelected;
        }

        private void OnCommandListKeyDown(object? sender, KeyEventArgs e)
        {
            if (ViewModel == null) return;

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                e.Handled = true;
                ViewModel.GoBackCommand?.Execute(null);
            }
            else if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;
                ViewModel.SaveCommandCommand?.Execute(null);
            }
        }

        private async void OnCommandSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
                await WavSounds.Click();
        }

        private async void OnCommandTapped(object sender, TappedEventArgs e)
        {
            await WavSounds.OK();
        }

        private void FocusCommandList()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (CommandListBox != null && CommandListBox.IsVisible)
                {
                    if (CommandListBox.SelectedIndex < 0)
                        CommandListBox.SelectedIndex = 0;

                    CommandListBox.Focus();
                }
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void OnInsertVariableClicked(object? sender, RoutedEventArgs e)
        {
            TemplateVariablePickerOverlay.Show();
            e.Handled = true;
        }

        private void OnTemplateVariableSelected(object? sender, string variable)
        {
            if (ArgumentsTextBox != null)
            {
                int caretIndex = ArgumentsTextBox.CaretIndex;
                string currentText = ArgumentsTextBox.Text ?? string.Empty;
                string newText = currentText.Insert(caretIndex, variable);
                ArgumentsTextBox.Text = newText;
                ArgumentsTextBox.CaretIndex = caretIndex + variable.Length;
                ArgumentsTextBox.Focus();
            }
        }
    }
}