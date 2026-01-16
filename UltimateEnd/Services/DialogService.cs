using System;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Enums;

namespace UltimateEnd.Services
{
    public class DialogService
    {
        private static DialogService? _instance;

        public static DialogService Instance => _instance ??= new DialogService();

        private Func<string, string, MessageType, Task>? _showMessage;
        private Func<string, string, Task<bool>>? _showConfirm;
        private Func<string, string, string, string, string, Task<int>>? _showThreeButton;
        private Func<string, CancellationTokenSource?, Task>? _showLoading;
        private Func<Task>? _hideLoading;
        private Func<string, Task>? _updateLoading;

        public void RegisterMessageOverlay(Func<string, string, MessageType, Task> showMessage)=> _showMessage = showMessage;

        public void RegisterConfirmOverlay(Func<string, string, Task<bool>> showConfirm) => _showConfirm = showConfirm;

        public void RegisterThreeButtonOverlay(Func<string, string, string, string, string, Task<int>> showThreeButton) => _showThreeButton = showThreeButton;

        public void RegisterLoadingOverlay(Func<string, CancellationTokenSource?, Task> showLoading, Func<Task> hideLoading, Func<string, Task> updateLoading)
        {
            _showLoading = showLoading;
            _hideLoading = hideLoading;
            _updateLoading = updateLoading;
        }

        public Task ShowError(string message) => ShowMessage("오류", message, MessageType.Error);

        public Task ShowWarning(string message) => ShowMessage("경고", message, MessageType.Warning);

        public Task ShowSuccess(string message) => ShowMessage("성공", message, MessageType.Success);

        public Task ShowInfo(string message) => ShowMessage("알림", message, MessageType.Info);

        public Task ShowMessage(string title, string message, MessageType type = MessageType.Info)
        {
            if (_showMessage == null) throw new InvalidOperationException("MessageOverlay가 등록되지 않았습니다.");

            return _showMessage(title, message, type);
        }

        public Task<bool> ShowConfirm(string title, string message)
        {
            if (_showConfirm == null) throw new InvalidOperationException("ConfirmOverlay가 등록되지 않았습니다.");

            return _showConfirm(title, message);
        }

        public Task<int> ShowThreeButton(string title, string message, string button1Text, string button2Text, string button3Text)
        {
            if (_showThreeButton == null) throw new InvalidOperationException("ThreeButtonOverlay가 등록되지 않았습니다.");

            return _showThreeButton(title, message, button1Text, button2Text, button3Text);
        }

        public Task ShowLoading(string message = "로딩 중...", CancellationTokenSource? cts = null)
        {
            if (_showLoading == null) throw new InvalidOperationException("LoadingOverlay가 등록되지 않았습니다.");

            return _showLoading(message, cts);
        }

        public Task UpdateLoading(string message)
        {
            if (_updateLoading == null) throw new InvalidOperationException("LoadingOverlay가 등록되지 않았습니다.");

            return _updateLoading(message);
        }

        public Task HideLoading()
        {
            if (_hideLoading == null) throw new InvalidOperationException("LoadingOverlay가 등록되지 않았습니다.");

            return _hideLoading();
        }
    }
}