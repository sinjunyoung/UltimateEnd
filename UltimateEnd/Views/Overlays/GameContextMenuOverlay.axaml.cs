using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Scraper;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class GameContextMenuOverlay : BaseOverlay
    {
        public event EventHandler? LaunchGameRequested;
        public event EventHandler? SetEmulatorRequested;
        public event EventHandler? SetSaveFileUploadRequested;
        public event EventHandler? SetSaveFileDownloadRequested;
        public event EventHandler? ToggleFavoriteRequested;
        public event EventHandler? AddToPlaylistRequested;
        public event EventHandler? RenameGameRequested;
        public event EventHandler? ChangeGenreRequested;
        public event EventHandler? ToggleKoreanRequested;
        public event EventHandler? SetLogoImageRequested;
        public event EventHandler? SetCoverImageRequested;
        public event EventHandler? SetGameVideoRequested;
        public event EventHandler? ToggleIgnoreRequested;
        public event EventHandler? ScrapStarting;
        public event EventHandler? ScrapEnded;
        public event EventHandler? EditScrapHintRequested;

        private int _selectedIndex = 0;
        private GameMetadata? _selectedGame = null;
        private readonly List<Border> _menuItems = [];
        private Dictionary<string, Action> _menuActions = [];
        private CancellationTokenSource? _currentOperationCts;

        public GameMetadata? SelectedGame
        {
            get => _selectedGame;
            set
            {
                _selectedGame = value;
                TitleText.Text = _selectedGame.DisplayTitle ?? "게임 메뉴";
            }
        }

        public override bool Visible => MainGrid.IsVisible;

        public GameContextMenuOverlay()
        {
            InitializeComponent();
            InitializeMenuActions();
        }

        private void InitializeMenuActions()
        {
            _menuActions = new Dictionary<string, Action>
            {
                ["LaunchGameItem"] = () => LaunchGameRequested?.Invoke(this, EventArgs.Empty),
                ["SetEmulatorItem"] = () => SetEmulatorRequested?.Invoke(this, EventArgs.Empty),
                ["SetSaveFileUploadItem"] = () => SetSaveFileUploadRequested?.Invoke(this, EventArgs.Empty),
                ["SetSaveFileDownloadItem"] = () => SetSaveFileDownloadRequested?.Invoke(this, EventArgs.Empty),
                ["ToggleFavoriteItem"] = () =>
                {
                    ToggleFavoriteRequested?.Invoke(this, EventArgs.Empty);
                    if (SelectedGame != null)
                        UpdateToggle(FavoriteToggle, FavoriteToggleThumb, SelectedGame.IsFavorite);
                },
                ["AddToPlaylistItem"] = () => ShowPlaylistSelection(),
                ["ScrapItem"] = async () => await OnScrapGameAsync(),
                ["ScrapHintEditItem"] = () => EditScrapHintRequested?.Invoke(this, EventArgs.Empty),
                ["RenameGameItem"] = () => RenameGameRequested?.Invoke(this, EventArgs.Empty),
                ["ChangeGenreItem"] = () => ChangeGenreRequested?.Invoke(this, EventArgs.Empty),
                ["ToggleKoreanItem"] = () =>
                {
                    if (SelectedGame != null)
                    {
                        SelectedGame.HasKorean = !SelectedGame.HasKorean;
                        UpdateToggle(KoreanToggle, KoreanToggleThumb, SelectedGame.HasKorean);
                    }

                    ToggleKoreanRequested?.Invoke(this, EventArgs.Empty);
                },
                ["SetLogoImageItem"] = () => SetLogoImageRequested?.Invoke(this, EventArgs.Empty),
                ["SetCoverImageItem"] = () => SetCoverImageRequested?.Invoke(this, EventArgs.Empty),
                ["SetGameVideoItem"] = () => SetGameVideoRequested?.Invoke(this, EventArgs.Empty),
                ["ToggleIgnoreItem"] = () =>
                {
                    ToggleKoreanRequested?.Invoke(this, EventArgs.Empty);

                    if (SelectedGame != null)
                    {
                        SelectedGame.Ignore = !SelectedGame.Ignore;
                        UpdateToggle(IgnoreToggle, IgnoreToggleThumb, SelectedGame.Ignore);
                    }
                }
            };
        }

        private void UpdateSelectedIndexFromSender(object? sender)
        {
            if (sender is Border border && _menuItems.Count > 0)
            {
                var index = _menuItems.IndexOf(border);

                if (index >= 0)
                {
                    _selectedIndex = index;
                    UpdateSelection();
                }
            }
        }

        protected override void MovePrevious()
        {
            if (_menuItems.Count == 0) return;

            _selectedIndex = (_selectedIndex - 1 + _menuItems.Count) % _menuItems.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_menuItems.Count == 0) return;

            _selectedIndex = (_selectedIndex + 1) % _menuItems.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_menuItems.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _menuItems.Count)
            {
                var selected = _menuItems[_selectedIndex];

                if (selected.Name != null && _menuActions.TryGetValue(selected.Name, out var action)) action?.Invoke();
            }
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < _menuItems.Count; i++)
            {
                var item = _menuItems[i];

                if (i == _selectedIndex)
                {
                    item.Background = this.FindResource("Background.Hover") as IBrush;
                    item.BringIntoView();
                }
                else
                    item.Background = this.FindResource("Background.Secondary") as IBrush;
            }
        }

        private void InitializeMenuItems()
        {
            if (_menuItems.Count > 0) return;

            var items = GameContextMenuPanel?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Name != null && b.Name.EndsWith("Item"))
                .ToList();

            if (items != null) _menuItems.AddRange(items);
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            MainGrid.IsVisible = true;

            this.Focusable = true;
            this.Focus();

            UpdateToggle(FavoriteToggle, FavoriteToggleThumb, SelectedGame.IsFavorite);
            UpdateToggle(KoreanToggle, KoreanToggleThumb, SelectedGame.HasKorean);
            UpdateToggle(IgnoreToggle, IgnoreToggleThumb, SelectedGame.Ignore);

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                InitializeMenuItems();
                UpdateSelection();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private static void UpdateToggle(Border toggleBack, Border toggle, bool value)
        {
            string resourceKey = value ? "Toggle.SelectionBackground" : "Toggle.Background";

            if (Avalonia.Application.Current != null && Avalonia.Application.Current.Resources.TryGetResource(resourceKey, Avalonia.Application.Current?.ActualThemeVariant, out object? resourceObj))
                toggleBack.Background = resourceObj as IBrush;

            toggle.HorizontalAlignment = value ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        }

        public override void Hide(HiddenState state)
        {
            MainGrid.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnLaunchGame(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            LaunchGameRequested?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void OnSetEmulator(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            SetEmulatorRequested?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void OnSetSaveFileUpload(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            SetSaveFileUploadRequested?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void OnSetSaveFileDownload(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            SetSaveFileDownloadRequested?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void OnToggleFavorite(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            ToggleFavoriteRequested?.Invoke(this, EventArgs.Empty);

            if (SelectedGame != null)
                UpdateToggle(FavoriteToggle, FavoriteToggleThumb, SelectedGame.IsFavorite);

            e.Handled = true;
        }

        private void OnRenameGame(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            RenameGameRequested?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void OnChangeGenre(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            ChangeGenreRequested?.Invoke(this, EventArgs.Empty);

            e.Handled = true;
        }

        private void OnToggleKorean(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            ToggleKoreanRequested?.Invoke(this, EventArgs.Empty);

            if (SelectedGame != null)
            {
                SelectedGame.HasKorean = !SelectedGame.HasKorean;
                UpdateToggle(KoreanToggle, KoreanToggleThumb, SelectedGame.HasKorean);
            }

            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible)
            {
                base.OnKeyDown(e);

                return;
            }

            if (InputManager.IsButtonPressed(e, GamepadButton.ButtonB) && _currentOperationCts != null && !_currentOperationCts.IsCancellationRequested)
            {
                _currentOperationCts.Cancel();
                e.Handled = true;

                return;
            }

            base.OnKeyDown(e);
        }

        private void ShowPlaylistSelection() => AddToPlaylistRequested?.Invoke(this, EventArgs.Empty);

        private void OnAddToPlaylist(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            ShowPlaylistSelection();
            e.Handled = true;
        }

        private async void OnScrapGame(object? sender, RoutedEventArgs e)
        {
            await WavSounds.OK();

            UpdateSelectedIndexFromSender(sender);
            e.Handled = true;

            await OnScrapGameAsync();
        }

        private async Task OnScrapGameAsync()
        {
            if (SelectedGame == null) return;

            if (!await ShouldProceedWithScrap(SelectedGame)) return;

            ScrapStarting?.Invoke(this, EventArgs.Empty);
            _currentOperationCts?.Cancel();
            _currentOperationCts?.Dispose();
            _currentOperationCts = new CancellationTokenSource();

            this.ShowLoading("준비 중...", _currentOperationCts);

            try
            {
                ScreenScraperService service = new();

                var screenScraperSystemId = PlatformInfoService.Instance.GetScreenScraperSystemId(SelectedGame.PlatformId);

                if (screenScraperSystemId == ScreenScraperSystemId.NotSupported)
                {
                    await DialogService.Instance.ShowWarning($"스크래핑 지원 플랫폼이 아닙니다.\n\n파일명: {SelectedGame.RomFile}");

                    return;
                }

                AutoScrapService.Instance.Stop();

                var result = await service.ScrapGameAsync(SelectedGame, screenScraperSystemId, message => this.ShowLoading(message), _currentOperationCts.Token);
                switch (result.ResultType)
                {
                    case ScrapResultType.Success:
                        {
                            await WavSounds.Coin();
                            var message = "스크랩 완료!";
                            if (result.Warnings.Count > 0)
                                message += $"\n경고: {string.Join(", ", result.Warnings)}";
                            await DialogService.Instance.ShowInfo(message);
                        }
                        break;

                    case ScrapResultType.Cached:
                        {
                            var message = "캐시에서 불러왔습니다!";
                            if (result.Warnings.Count > 0)
                                message += $"\n경고: {string.Join(", ", result.Warnings)}";
                            await DialogService.Instance.ShowInfo(message);
                        }
                        break;

                    case ScrapResultType.ApiLimitExceeded:
                        await DialogService.Instance.ShowError($"{result.Message}\n\n잠시 후 다시 시도해주세요.");
                        break;

                    case ScrapResultType.NotFound:
                        await DialogService.Instance.ShowWarning($"검색 결과를 찾을 수 없습니다.\n\n파일명: {SelectedGame.RomFile}");
                        break;

                    case ScrapResultType.NetworkError:
                        await DialogService.Instance.ShowError($"네트워크 오류가 발생했습니다.\n\n{result.Message}\n\n인터넷 연결을 확인해주세요.");
                        break;

                    case ScrapResultType.InvalidFile:
                        await DialogService.Instance.ShowError($"파일 오류가 발생했습니다.\n\n{result.Message}");
                        break;

                    case ScrapResultType.Cancelled:
                        break;

                    case ScrapResultType.Failed:
                    default:
                        await DialogService.Instance.ShowError($"스크랩에 실패했습니다.\n\n{result.Message}");
                        break;
                }
            }
            catch (Exception ex)
            {
                await DialogService.Instance.ShowError($"예기치 않은 오류가 발생했습니다.\n\n{ex.Message}");
            }
            finally
            {
                this.HideLoading();
                _currentOperationCts?.Dispose();
                _currentOperationCts = null;
                this.Hide(HiddenState.Silent);
                ScrapEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnEditScrapHint(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            EditScrapHintRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private static async Task<bool> ShouldProceedWithScrap(GameMetadata game)
        {
            var condition = ScreenScraperConfig.Instance.ScrapConditionType;

            if (condition == ScrapCondition.None) return true;

            bool hasLogo = game.HasLogoImage;
            bool hasCover = game.HasCoverImage;
            bool hasVideo = game.HasVideo;

            ScrapCondition existing = ScrapCondition.None;

            if (hasLogo) existing |= ScrapCondition.LogoMissing;
            if (hasCover) existing |= ScrapCondition.CoverMissing;
            if (hasVideo) existing |= ScrapCondition.VideoMissing;

            if ((condition & existing) != ScrapCondition.None)
            {
                var existingItems = new List<string>();

                if ((existing & ScrapCondition.LogoMissing) != 0) existingItems.Add("로고");
                if ((existing & ScrapCondition.CoverMissing) != 0) existingItems.Add("커버");
                if ((existing & ScrapCondition.VideoMissing) != 0) existingItems.Add("비디오");

                return await DialogService.Instance.ShowConfirm("확인", $"이미 {string.Join(", ", existingItems)}가 존재합니다.\n계속 진행하면 기존 파일이 교체됩니다.");
            }

            return true;
        }

        private void OnSetLogoImage(object? sender, RoutedEventArgs e)
        {
            SetLogoImageRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnSetCoverImage(object? sender, RoutedEventArgs e)
        {
            SetCoverImageRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnSetGameVideo(object? sender, RoutedEventArgs e)
        {
            SetGameVideoRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnToggleIgnore(object? sender, RoutedEventArgs e)
        {
            UpdateSelectedIndexFromSender(sender);
            ToggleIgnoreRequested?.Invoke(this, EventArgs.Empty);

            if (SelectedGame != null)
            {
                SelectedGame.Ignore = !SelectedGame.Ignore;
                UpdateToggle(IgnoreToggle, IgnoreToggleThumb, SelectedGame.Ignore);
            }

            e.Handled = true;
        }

        private void OnClose(object? sender, PointerPressedEventArgs e)
        {
            Hide(HiddenState.Close);
            e.Handled = true;
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Hide(HiddenState.Close);
        }
    }
}
