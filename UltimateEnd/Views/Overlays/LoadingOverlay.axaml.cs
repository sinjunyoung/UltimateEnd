using Avalonia.Input;
using Avalonia.Threading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using UltimateEnd.Enums;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays;

public partial class LoadingOverlay : BaseOverlay, INotifyPropertyChanged
{
    private bool _isLoading;
    private string _message = "로딩 중...";
    private CancellationTokenSource? _cts;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Visible));
            }
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            if (_message != value)
            {
                _message = value;
                OnPropertyChanged();
            }
        }
    }

    public override bool Visible => IsLoading;

    public LoadingOverlay()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void Show(string message = "로딩 중...", CancellationTokenSource? cts = null)
    {
        Message = message;

        if (cts != null)
            _cts = cts;

        IsLoading = true;
        IsVisible = true;

        this.Focusable = true;

        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }

    public void Hide()
    {
        IsLoading = false;
        IsVisible = false;
        _cts = null;
    }

    private async void Cancel()
    {
        if (_cts == null || _cts.IsCancellationRequested) return;

        await WavSounds.Cancel();
        _cts.Cancel();
        Message = "취소 중...";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsLoading)
        {
            base.OnKeyDown(e);
            return;
        }

        if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
        {
            Cancel();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void MovePrevious() { }

    protected override void MoveNext() { }

    protected override void SelectCurrent() => Cancel();

    public new event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public override void Hide(HiddenState state)
    {
        if (state == HiddenState.Cancel)
        {
            Cancel();
            OnHidden(new HiddenEventArgs { State = HiddenState.Cancel });
        }
        else
        {
            Hide();
            OnHidden(new HiddenEventArgs { State = HiddenState.Close });
        }
    }

    public override void Show() => Show("로딩 중...");

    public void UpdateMessage(string message) => Message = message;
}