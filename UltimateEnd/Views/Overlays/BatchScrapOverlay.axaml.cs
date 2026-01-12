using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Scraper;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class BatchScrapOverlay : BaseOverlay, INotifyPropertyChanged, IDisposable
    {
        private CancellationTokenSource _cts;
        private bool _visible;
        private bool _isScrapInProgress;
        private int _totalCount;
        private int _currentCount;
        private int _successCount;
        private int _failedCount;
        private int _skippedCount;
        private int _cachedCount;
        private string _currentStatus = string.Empty;
        private double _progressPercentage;
        private string _elapsedTime = string.Empty;
        private Stopwatch _stopwatch;
        private Timer _elapsedTimer;


        private Bitmap _currentCoverImage;
        private string _currentGameTitle = string.Empty;
        private string _currentGameDescription = string.Empty;

        public Bitmap CurrentCoverImage
        {
            get => _currentCoverImage;
            set => SetProperty(ref _currentCoverImage, value);
        }

        public string CurrentGameTitle
        {
            get => _currentGameTitle;
            set => SetProperty(ref _currentGameTitle, value);
        }

        public string CurrentGameDescription
        {
            get => _currentGameDescription;
            set => SetProperty(ref _currentGameDescription, value);
        }

        public BatchScrapOverlay()
        {
            InitializeComponent();
            DataContext = this;
            FailedItems = [];
        }

        #region Properties

        public override bool Visible => _visible;

        private bool InternalVisible
        {
            get => _visible;
            set
            {
                if (SetProperty(ref _visible, value))
                {
                    IsVisible = value;
                    OnPropertyChanged(nameof(Visible));
                }
            }
        }

        public bool IsScrapInProgress
        {
            get => _isScrapInProgress;
            set => SetProperty(ref _isScrapInProgress, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public int CurrentCount
        {
            get => _currentCount;
            set
            {
                if (SetProperty(ref _currentCount, value))
                    UpdateProgressPercentage();
            }
        }

        public int SuccessCount
        {
            get => _successCount;
            set => SetProperty(ref _successCount, value);
        }

        public int FailedCount
        {
            get => _failedCount;
            set => SetProperty(ref _failedCount, value);
        }

        public int SkippedCount
        {
            get => _skippedCount;
            set => SetProperty(ref _skippedCount, value);
        }

        public int CachedCount
        {
            get => _cachedCount;
            set => SetProperty(ref _cachedCount, value);
        }

        public string CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        public string ElapsedTime
        {
            get => _elapsedTime;
            set => SetProperty(ref _elapsedTime, value);
        }

        public bool HasFailedItems => FailedItems?.Count > 0;

        public ObservableCollection<FailedScrapItem> FailedItems { get; }

        #endregion

        #region Public Methods

        public override void Show()
        {
            InternalVisible = true;
            OnShowing(EventArgs.Empty);

            this.Focusable = true;
            Dispatcher.UIThread.Post(() => this.Focus(), DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            if (state == HiddenState.Cancel && IsScrapInProgress)
            {
                Cancel();
                return;
            }

            StopElapsedTimer();
            InternalVisible = false;

            OnHidden(new HiddenEventArgs { State = state });
        }

        public async Task<bool> StartBatchScrapAsync(ScreenScraperService service, List<GameMetadata> games)
        {
            if (IsScrapInProgress)
                return false;

            ArgumentNullException.ThrowIfNull(service);

            if (games == null || games.Count == 0)
            {
                CurrentStatus = "스크랩할 게임이 없습니다";
                return false;
            }

            Show();
            IsScrapInProgress = true;
            TotalCount = games.Count;
            CurrentCount = 0;
            SuccessCount = 0;
            FailedCount = 0;
            CachedCount = 0;
            SkippedCount = 0;
            CurrentStatus = "준비 중...";
            ElapsedTime = "0.0초";

            FailedItems.Clear();
            OnPropertyChanged(nameof(HasFailedItems));

            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _stopwatch = Stopwatch.StartNew();
            StartElapsedTimer();

            bool hasAnySuccess = false;
            bool wasCancelled = false;
            bool hasApiLimitError = false;

            CurrentCoverImage = null;

            try
            {
                var progress = new Progress<BatchProgress>(p =>
                {
                    CurrentCount = p.Current;
                    TotalCount = p.Total;
                    SuccessCount = p.Success;
                    FailedCount = p.Failed;
                    CachedCount = p.Cached;
                    SkippedCount = p.Skipped;
                    CurrentStatus = p.Status;

                    if (p.SuccessGame != null)
                    {
                        _ = WavSounds.Coin();
                        UpdateCoverPreview(p.SuccessGame);
                    }
                });

                AutoScrapService.Instance.Stop();
                var result = await service.BatchScrapGamesAsync(games, progress, _cts.Token);

                foreach (var (romPath, error) in result.Failures)
                {
                    if (error.Contains("API 호출 제한") ||
                        error.Contains("quota") ||
                        error.Contains("exceeded"))
                    {
                        hasApiLimitError = true;
                    }

                    FailedItems.Add(new FailedScrapItem
                    {
                        FileName = System.IO.Path.GetFileName(romPath),
                        FullPath = romPath,
                        ErrorMessage = error
                    });
                }

                if (FailedItems.Count > 0)
                {
                    OnPropertyChanged(nameof(HasFailedItems));
                    OnPropertyChanged(nameof(FailedItems));
                }

                hasAnySuccess = result.SuccessCount > 0;
                wasCancelled = _cts.Token.IsCancellationRequested;

                if (wasCancelled)
                    CurrentStatus = $"작업 취소됨 (성공: {result.SuccessCount}, 건너뜀: {result.SkippedCount}, 실패: {result.FailedCount})";
                else if (hasApiLimitError)
                    CurrentStatus = $"API 제한 도달 (성공: {result.SuccessCount}, 건너뜀: {result.SkippedCount}, 실패: {result.FailedCount})";
                else if (result.FailedCount == 0 && result.SkippedCount == 0)
                    CurrentStatus = $"모든 작업 완료! (성공: {result.SuccessCount})";
                else if (result.SuccessCount == 0)
                    CurrentStatus = $"모든 작업 실패 (건너뜀: {result.SkippedCount}, 실패: {result.FailedCount})";
                else
                    CurrentStatus = $"작업 완료 (성공: {result.SuccessCount}, 건너뜀: {result.SkippedCount}, 실패: {result.FailedCount})";

                if (hasAnySuccess)
                {
                    int duration = WavSounds.Durations.Complete;

                    if (hasAnySuccess)
                    {
                        await WavSounds.Complete();
                        await Task.Delay(WavSounds.Durations.Complete);
                    }
                }

                return hasAnySuccess;
            }
            catch (OperationCanceledException)
            {
                CurrentStatus = $"작업 취소됨 (성공: {SuccessCount}, 건너뜀: {SkippedCount}, 실패: {FailedCount})";
                return hasAnySuccess;
            }
            catch (Exception ex)
            {
                CurrentStatus = $"오류 발생: {ex.Message}";

                if (FailedItems.Count == 0)
                {
                    FailedItems.Add(new FailedScrapItem
                    {
                        FileName = "시스템 오류",
                        FullPath = string.Empty,
                        ErrorMessage = ex.Message
                    });
                    OnPropertyChanged(nameof(HasFailedItems));
                }

                return hasAnySuccess;
            }
            finally
            {
                IsScrapInProgress = false;
                StopElapsedTimer();

                _cts?.Dispose();
                _cts = null;

                _stopwatch?.Stop();
                UpdateElapsedTime();
            }
        }

        #endregion

        #region Private Methods

        private void UpdateCoverPreview(GameMetadata game)
        {
            try
            {
                CurrentGameTitle = game.DisplayTitle;
                CurrentGameDescription = game.DisplayDescription;

                var coverPath = game.GetCoverPath();

                if (!string.IsNullOrEmpty(coverPath) && System.IO.File.Exists(coverPath))
                {
                    using var stream = new System.IO.FileStream(coverPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
                    CurrentCoverImage = new Bitmap(stream);
                }
                else
                    CurrentCoverImage = null;
            }
            catch
            {
                CurrentCoverImage = null;
            }
        }

        private void UpdateProgressPercentage()
        {
            if (TotalCount > 0)
                ProgressPercentage = (double)CurrentCount / TotalCount * 100;
            else
                ProgressPercentage = 0;
        }

        private void StartElapsedTimer()
        {
            StopElapsedTimer();

            _elapsedTimer = new Timer(_ =>
            {
                Dispatcher.UIThread.Post(UpdateElapsedTime);
            }, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
        }

        private void StopElapsedTimer()
        {
            _elapsedTimer?.Dispose();
            _elapsedTimer = null;
        }

        private void UpdateElapsedTime()
        {
            if (_stopwatch == null)
                return;

            var elapsed = _stopwatch.Elapsed;

            if (elapsed.TotalSeconds < 60)
                ElapsedTime = $"{elapsed.TotalSeconds:F1}초";
            else if (elapsed.TotalMinutes < 60)
                ElapsedTime = $"{elapsed.TotalMinutes:F1}분";
            else
                ElapsedTime = $"{elapsed.TotalHours:F1}시간";
        }

        private async void Cancel()
        {
            if (_cts == null || _cts.IsCancellationRequested)
                return;

            await WavSounds.Cancel();

            _cts.Cancel();
            CurrentStatus = "취소 중...";
        }

        private async void Close()
        {
            if (IsScrapInProgress)
            {
                await WavSounds.Cancel();
                return;
            }

            await WavSounds.Cancel();
            Hide(HiddenState.Close);
        }

        #endregion

        #region Key Input Handling

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible)
            {
                base.OnKeyDown(e);
                return;
            }

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                if (IsScrapInProgress)
                    Cancel();
                else
                    Close();

                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        protected override void MovePrevious() { }

        protected override void MoveNext() { }

        protected override void SelectCurrent()
        {
            if (IsScrapInProgress)
                Cancel();
            else
                Close();
        }

        #endregion

        #region Event Handlers

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Cancel();
            e.Handled = true;
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
            e.Handled = true;
        }

        #endregion

        #region INotifyPropertyChanged

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion

        #region IDisposable

        public void Dispose()
        {
            StopElapsedTimer();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion
    }
}