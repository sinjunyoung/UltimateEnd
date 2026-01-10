using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Helpers;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class MessageOverlay : BaseOverlay
    {
        private TaskCompletionSource<bool>? _tcs;
        private TaskCompletionSource<int>? _tcsThreeButton;
        private bool _isConfirmMode = false;
        private bool _isThreeButtonMode = false;
        private readonly Stack<FocusSnapshot?> _focusStack = new();

        public override bool Visible => MainGrid.IsVisible;

        public MessageOverlay() => InitializeComponent();

        public Task ShowMessage(string title, string message, MessageType type = MessageType.Info)
        {
            _tcs = new TaskCompletionSource<bool>();
            _isConfirmMode = false;
            _isThreeButtonMode = false;

            var snapshot = FocusHelper.CreateSnapshot();
            _focusStack.Push(snapshot);

            _focusStack.Push(FocusHelper.CreateSnapshot());

            TitleText.Text = title;
            MessageText.Text = message;

            OkButton.IsVisible = true;
            CancelButton.IsVisible = false;
            ConfirmButton.IsVisible = false;
            ThreeButtonPanel.IsVisible = false;

            _ = UpdateMessageStyle(type);
            Show();

            return _tcs.Task;
        }

        public Task<bool> ShowConfirm(string title, string message)
        {
            _tcs = new TaskCompletionSource<bool>();
            _isConfirmMode = true;
            _isThreeButtonMode = false;

            _focusStack.Push(FocusHelper.CreateSnapshot());

            TitleText.Text = title;
            MessageText.Text = message;

            OkButton.IsVisible = false;
            CancelButton.IsVisible = true;
            ConfirmButton.IsVisible = true;
            ThreeButtonPanel.IsVisible = false;

            _ = UpdateConfirmStyle();
            Show();

            return _tcs.Task;
        }

        public Task<int> ShowThreeButton(string title, string message, string button1Text, string button2Text, string button3Text)
        {
            _tcsThreeButton = new TaskCompletionSource<int>();
            _isConfirmMode = false;
            _isThreeButtonMode = true;

            _focusStack.Push(FocusHelper.CreateSnapshot());

            TitleText.Text = title;
            MessageText.Text = message;

            OkButton.IsVisible = false;
            CancelButton.IsVisible = false;
            ConfirmButton.IsVisible = false;
            ThreeButtonPanel.IsVisible = true;

            Button1Text.Text = button1Text;
            Button2Text.Text = button2Text;
            Button3Text.Text = button3Text;

            _ = UpdateThreeButtonStyle();
            Show();

            return _tcsThreeButton.Task;
        }

        private async Task UpdateMessageStyle(MessageType type)
        {
            IBrush? iconBrush = null;
            string iconData = string.Empty;

            switch (type)
            {
                case MessageType.Info:
                    iconData = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M13,17H11V11H13M13,9H11V7H13";
                    iconBrush = new SolidColorBrush(Color.Parse("#2196F3"));
                    await WavSounds.Success();
                    break;
                case MessageType.Error:
                    iconData = "M12,2C17.53,2 22,6.47 22,12C22,17.53 17.53,22 12,22C6.47,22 2,17.53 2,12C2,6.47 6.47,2 12,2M15.59,7L12,10.59L8.41,7L7,8.41L10.59,12L7,15.59L8.41,17L12,13.41L15.59,17L17,15.59L13.41,12L17,8.41";
                    iconBrush = new SolidColorBrush(Color.Parse("#F44336"));
                    await WavSounds.Error();
                    break;
                case MessageType.Warning:
                    iconData = "M13,14H11V10H13M13,18H11V16H13M1,21H23L12,2L1,21Z";
                    iconBrush = new SolidColorBrush(Color.Parse("#FF9800"));
                    await WavSounds.Error();
                    break;
                case MessageType.Success:
                    iconData = "M12,2C6.47,2 2,6.47 2,12C2,17.53 6.47,22 12,22C17.53,22 22,17.53 22,12C22,6.47 17.53,2 12,2M10.5,17L5.5,12L6.91,10.59L10.5,14.17L17.09,7.59L18.5,9";
                    iconBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
                    await WavSounds.Success();
                    break;
            }

            IconPath.Data = Geometry.Parse(iconData);
            IconPath.Fill = iconBrush;
            TitleText.Foreground = iconBrush;
        }

        private async Task UpdateConfirmStyle()
        {
            string iconData = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M13,17H11V15H13M13,13H11C11,10.76 13.5,11 13.5,9C13.5,7.9 12.6,7 11.5,7C10.4,7 9.5,7.9 9.5,9H7.5C7.5,6.79 9.29,5 11.5,5C13.71,5 15.5,6.79 15.5,9C15.5,11.5 13,11.75 13,13Z";
            IBrush iconBrush = new SolidColorBrush(Color.Parse("#FF9800"));

            IconPath.Data = Geometry.Parse(iconData);
            IconPath.Fill = iconBrush;
            TitleText.Foreground = iconBrush;

            await WavSounds.Click();
        }

        private async Task UpdateThreeButtonStyle()
        {
            string iconData = "M13,14H11V10H13M13,18H11V16H13M1,21H23L12,2L1,21Z";
            IBrush iconBrush = new SolidColorBrush(Color.Parse("#FF9800"));

            IconPath.Data = Geometry.Parse(iconData);
            IconPath.Fill = iconBrush;
            TitleText.Foreground = iconBrush;

            await WavSounds.Click();
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

            if (_focusStack.Count > 0)
            {
                var snapshot = _focusStack.Pop();
                FocusHelper.SetFocusImmediate(snapshot?.SavedElement);
            }

            if (_isThreeButtonMode)
            {
            }
            else if (_isConfirmMode)
                _tcs?.TrySetResult(state == HiddenState.Confirm);
            else
                _tcs?.TrySetResult(true);

            OnHidden(new HiddenEventArgs { State = state });
        }

        protected override void SelectCurrent()
        {
            if (_isConfirmMode)
                OnConfirmClick(null, null);
            else if (_isThreeButtonMode)
                OnButton3Click(null, null);
            else
                Hide(HiddenState.Confirm);
        }

        private async void OnOkClick(object? sender, PointerPressedEventArgs e)
        {
            await WavSounds.OK();
            Hide(HiddenState.Confirm);
            e.Handled = true;
        }

        private async void OnConfirmClick(object? sender, PointerPressedEventArgs? e)
        {
            await WavSounds.OK();
            Hide(HiddenState.Confirm);
            if (e != null) e.Handled = true;
        }

        private async void OnCancelClick(object? sender, PointerPressedEventArgs e)
        {
            await WavSounds.Cancel();
            Hide(HiddenState.Cancel);
            e.Handled = true;
        }

        private async void OnButton1Click(object? sender, PointerPressedEventArgs? e)
        {
            await WavSounds.OK();
            MainGrid.IsVisible = false;

            if (_focusStack.Count > 0)
            {
                var snapshot = _focusStack.Pop();
                FocusHelper.SetFocusImmediate(snapshot?.SavedElement);
            }

            _tcsThreeButton?.TrySetResult(0);
            OnHidden(new HiddenEventArgs { State = HiddenState.Confirm });
            if (e != null) e.Handled = true;
        }

        private async void OnButton2Click(object? sender, PointerPressedEventArgs? e)
        {
            await WavSounds.OK();
            MainGrid.IsVisible = false;

            if (_focusStack.Count > 0)
            {
                var snapshot = _focusStack.Pop();
                FocusHelper.SetFocusImmediate(snapshot?.SavedElement);
            }

            _tcsThreeButton?.TrySetResult(1);
            OnHidden(new HiddenEventArgs { State = HiddenState.Confirm });
            if (e != null) e.Handled = true;
        }

        private async void OnButton3Click(object? sender, PointerPressedEventArgs? e)
        {
            await WavSounds.Cancel();
            MainGrid.IsVisible = false;

            if (_focusStack.Count > 0)
            {
                var snapshot = _focusStack.Pop();
                FocusHelper.SetFocusImmediate(snapshot?.SavedElement);
            }

            _tcsThreeButton?.TrySetResult(2);
            OnHidden(new HiddenEventArgs { State = HiddenState.Cancel });
            if (e != null) e.Handled = true;
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
            {
                _ = WavSounds.Cancel();

                if (_isThreeButtonMode)
                {
                    MainGrid.IsVisible = false;

                    if (_focusStack.Count > 0)
                    {
                        var snapshot = _focusStack.Pop();
                        FocusHelper.SetFocusImmediate(snapshot?.SavedElement);
                    }

                    _tcsThreeButton?.TrySetResult(2);
                    OnHidden(new HiddenEventArgs { State = HiddenState.Cancel });
                }
                else if (_isConfirmMode)
                    Hide(HiddenState.Cancel);
                else
                    Hide(HiddenState.Close);

                e.Handled = true;
            }
        }
    }
}