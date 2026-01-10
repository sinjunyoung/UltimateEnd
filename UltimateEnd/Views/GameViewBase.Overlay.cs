using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Helpers;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.SaveFile;
using UltimateEnd.Scraper;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.Views.Helpers;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public abstract partial class GameViewBase
    {
        #region Abstract Overlay Properties

        protected abstract EmulatorSelectionOverlay EmulatorOverlayBase { get; }

        protected abstract PlaylistSelectionOverlay PlaylistSelectionOverlayBase { get; }

        protected abstract GameContextMenuOverlay GameContextMenuOverlayBase { get; }

        protected abstract GameEmulatorSelectionOverlay GameEmulatorOverlayBase { get; }

        protected abstract GameGenreOverlay GameGenreOverlayBase { get; }

        protected abstract GameRenameOverlay GameRenameOverlayBase { get; }

        protected abstract GenreFilterOverlay GenreFilterOverlayBase { get; }

        protected abstract SettingsMenuOverlay SettingsMenuOverlayBase { get; }

        protected abstract BackupListOverlay BackupListOverlayBase { get; }

        #endregion

        #region Overlay Initialization

        private void InitializeOverlayEvents()
        {
            RegisterOverlays();
            AttachCommonOverlayEvents();
            AttachSpecificOverlayEvents();
        }

        protected virtual void RegisterOverlays()
        {
            _overlays.Add("EmulatorSelectionOverlay", EmulatorOverlayBase);
            _overlays.Add("PlaylistSelectionOverlay", PlaylistSelectionOverlayBase);
            _overlays.Add("GameContextMenuOverlay", GameContextMenuOverlayBase);
            _overlays.Add("GameEmulatorSelectionOverlay", GameEmulatorOverlayBase);
            _overlays.Add("GameGenreOverlay", GameGenreOverlayBase);
            _overlays.Add("GameRenameOverlay", GameRenameOverlayBase);
            _overlays.Add("GenreFilterOverlay", GenreFilterOverlayBase);
            _overlays.Add("SettingsMenuOverlay", SettingsMenuOverlayBase);
            _overlays.Add("BackupListOverlay", BackupListOverlayBase);
        }

        private void AttachCommonOverlayEvents()
        {
            foreach (var overlay in _overlays.Values)
            {
                overlay.Showing += OnOverlayShowing;
                overlay.Hidden += OnOverlayHidden;
                overlay.Click += async (s, e) => await WavSounds.Click();
            }
        }

        private void AttachSpecificOverlayEvents()
        {
            SettingsMenuOverlayBase.EmulatorClicked += OnSettingsMenu_EmulatorClick;
            SettingsMenuOverlayBase.ScrapClicked += OnSettingsMenu_ScrapClicked;
            SettingsMenuOverlayBase.PlaylistClicked += OnSettingsMenu_PlaylistClicked;
            SettingsMenuOverlayBase.ManageIgnoreGameClicked += (sender, e) =>
            {
                ViewModel.IgnoreGameToggle();
                SettingsMenuOverlayBase.Hide(HiddenState.Close);
            };
            SettingsMenuOverlayBase.PlatformImageClicked += OnSettingsMenu_PlatformImageClick;
            SettingsMenuOverlayBase.PegasusMetadataClicked += OnSettingsMenu_PegasusMetadataLoad;
            SettingsMenuOverlayBase.EsDeMetadataClicked += OnSettingsMenu_EsDeMetadataLoad;

            EmulatorOverlayBase.EmulatorSelected += OnEmulator_Selected;
            GenreFilterOverlayBase.GenreSelected += OnGenreFilter_Selected;

            AttachGameContextMenuEvents();

            AttachGameEmulatorEvents();

            GameRenameOverlayBase.SaveRequested += OnGameRename_Save;
            GameGenreOverlayBase.GenreSelected += OnGameGenre_Selected;
        }

        private void AttachGameContextMenuEvents()
        {
            GameContextMenuOverlayBase.LaunchGameRequested += OnContextMenu_LaunchGame;
            GameContextMenuOverlayBase.SetEmulatorRequested += OnContextMenu_SetEmulator;
            GameContextMenuOverlayBase.SetSaveFileUploadRequested += OnContextMenu_SetSaveFileUpload;
            GameContextMenuOverlayBase.SetSaveFileDownloadRequested += OnContextMenu_SetSaveFileDownload;            
            GameContextMenuOverlayBase.ToggleFavoriteRequested += OnContextMenu_ToggleFavorite;
            GameContextMenuOverlayBase.AddToPlaylistRequested += OnContextMenu_AddToPlaylist;
            GameContextMenuOverlayBase.RenameGameRequested += OnContextMenu_RenameGame;
            GameContextMenuOverlayBase.ChangeGenreRequested += OnContextMenu_ChangeGenre;
            GameContextMenuOverlayBase.ToggleKoreanRequested += OnContextMenu_ToggleKorean;
            GameContextMenuOverlayBase.SetLogoImageRequested += OnContextMenu_SetLogoImage;
            GameContextMenuOverlayBase.SetCoverImageRequested += OnContextMenu_SetCoverImage;
            GameContextMenuOverlayBase.SetGameVideoRequested += OnContextMenu_SetGameVideo;
            GameContextMenuOverlayBase.ToggleIgnoreRequested += OnContextMenu_ToggleIgnore;

            GameContextMenuOverlayBase.ScrapStarting += OnContextMenu_ScrapStarting;
            GameContextMenuOverlayBase.ScrapEnded += OnContextMenu_ScrapEnded;
        }

        private void AttachGameEmulatorEvents()
        {
            GameEmulatorOverlayBase.DefaultSelected += OnGameEmulator_DefaultSelected;
            GameEmulatorOverlayBase.EmulatorSelected += OnGameEmulator_EmulatorSelected;
        }

        #endregion

        #region Common Overlay Events

        private async void OnOverlayHidden(object? sender, HiddenEventArgs e)
        {
            switch (e.State)
            {
                case HiddenState.Close:
                case HiddenState.Cancel:
                    await WavSounds.Cancel();
                    break;
                case HiddenState.Confirm:
                    await WavSounds.OK();
                    break;
                case HiddenState.Silent:
                    break;
            }

            var parentOverlay = GetParentOverlay(sender as BaseOverlay);

            if (parentOverlay != null && parentOverlay.Visible)
                parentOverlay.Focus();
            else
            {
                GameScrollViewerFocusLoaded();

                if (!_isLoadingMetadata) OnOverlayHiddenCore(e);
            }
        }

        private BaseOverlay? GetParentOverlay(BaseOverlay? childOverlay)
        {
            if (childOverlay == null) return null;

            if (childOverlay == EmulatorOverlayBase) return SettingsMenuOverlayBase.Visible ? SettingsMenuOverlayBase : null;
            if (childOverlay == PlaylistSelectionOverlayBase) return GameContextMenuOverlayBase.Visible ? GameContextMenuOverlayBase : null;
            if (childOverlay == GameEmulatorOverlayBase) return GameContextMenuOverlayBase.Visible ? GameContextMenuOverlayBase : null;
            if (childOverlay == GameRenameOverlayBase) return GameContextMenuOverlayBase.Visible ? GameContextMenuOverlayBase : null;
            if (childOverlay == GameGenreOverlayBase) return GameContextMenuOverlayBase.Visible ? GameContextMenuOverlayBase : null;
            if (childOverlay == BackupListOverlayBase) return GameContextMenuOverlayBase.Visible ? GameContextMenuOverlayBase : null;

            return null;
        }

        #endregion

        #region Settings Menu Overlay Events

        private async void OnSettingsMenu_EmulatorClick(object? sender, EventArgs e)
        {
            await WavSounds.OK();

            var availableEmulators = ViewModel?.GetAvailableEmulators();
            if (availableEmulators == null || availableEmulators.Count == 0)
            {
                SettingsMenuOverlayBase.Hide(HiddenState.Close);
                return;
            }

            if (!availableEmulators.Any(e => e.IsDefault) && availableEmulators.Count > 0)
                availableEmulators[0].IsDefault = true;

            EmulatorOverlayBase.SetEmulators(availableEmulators);
            EmulatorOverlayBase.Show();
        }

        private async void OnContextMenu_AddToPlaylist(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                await WavSounds.OK();
                PlaylistSelectionOverlayBase.ShowForGame(ViewModel.ContextMenuTargetGame);
            }
        }

        private async void OnSettingsMenu_ScrapClicked(object? sender, EventArgs e)
        {
            //var screenScraperSystemId = PlatformInfoService.GetScreenScraperSystemId(ViewModel.Platform.MappedPlatformId);

            //if (screenScraperSystemId == ScreenScraperSystemId.NotSupported)
            //{
            //    await DialogService.Instance.ShowWarning($"스크래핑 지원 플랫폼이 아닙니다");
            //    return;
            //}

            ViewModel.IsDownloadingMedia = true;

            await WavSounds.OK();

            ScreenSaverManager.Instance.PauseScreenSaver();

            OnScrapStarting();

            using ScreenScraperService scraperService = new();

            var batchOverlay = this.FindAncestorOfType<MainContentView>()?.BatchScrapOverlay;

            if (batchOverlay != null)
            {
                void handler(object? s, HiddenEventArgs e)
                {
                    batchOverlay.Hidden -= handler;
                    SettingsMenuOverlayBase.Hide(HiddenState.Close);

                    ViewModel.IsDownloadingMedia = false;
                    ScreenSaverManager.Instance.ResumeScreenSaver();

                    OnScrapEnded();
                }

                batchOverlay.Hidden += handler;
            }

            await this.StartBatchScrapAsync(scraperService, [.. ViewModel.Games]);
        }

        private async void OnSettingsMenu_PlaylistClicked(object? sender, EventArgs e)
        {
            await WavSounds.OK();
            PlaylistSelectionOverlayBase.ShowForGames(ViewModel.Games);
        }

        private async void OnSettingsMenu_PlatformImageClick(object? sender, EventArgs e)
        {
            if (ViewModel != null && !GameMetadataManager.IsSpecialPlatform(ViewModel.Platform.Id)) await ViewModel.SetPlatformImage();

            SettingsMenuOverlayBase.Hide(HiddenState.Close);
        }

        private async Task LoadMetadataAsync(Func<string, Task> loadFunc, string errorMessage)
        {
            if (ViewModel == null || GameMetadataManager.IsSpecialPlatform(ViewModel.Platform.Id))
            {
                await DialogService.Instance.ShowMessage("지원 불가 ❌", errorMessage, MessageType.Error);
                SettingsMenuOverlayBase.Hide(HiddenState.Close);
                return;
            }

            await WavSounds.OK();

            var converter = PathConverterFactory.Create?.Invoke();
            var initialDirectory = converter?.FriendlyPathToRealPath(ViewModel.Platform.FolderPath) ?? ViewModel.Platform.FolderPath;

            _isLoadingMetadata = true;
            SettingsMenuOverlayBase.Hide(HiddenState.Silent);

            try
            {
                await loadFunc(initialDirectory);
            }
            finally
            {
                _isLoadingMetadata = false;
                await Task.Delay(50);
                GameScrollViewerFocusLoaded();
            }
        }

        private async void OnSettingsMenu_PegasusMetadataLoad(object? sender, EventArgs e) => await LoadMetadataAsync(ViewModel.LoadPegasusMetadata, "이 플랫폼은 Pegasus 메타데이터 불러오기를 지원하지 않습니다.");

        private async void OnSettingsMenu_EsDeMetadataLoad(object? sender, EventArgs e) => await LoadMetadataAsync(ViewModel.LoadEsDeMetadata, "이 플랫폼은 ES-DE 메타데이터 불러오기를 지원하지 않습니다.");

        #endregion

        #region Emulator Overlay Events

        private void OnEmulator_Selected(object? sender, EmulatorInfo emulator)
        {
            ViewModel?.SetEmulator(emulator.Id);
            EmulatorOverlayBase.Hide(HiddenState.Confirm);
            SettingsMenuOverlayBase.Hide(HiddenState.Silent);
        }

        #endregion

        #region Genre Filter Overlay Events

        private void OnGenreFilter_Selected(object? sender, string genre)
        {
            ViewModel.SelectedGenre = genre;
            GenreFilterOverlayBase.Hide(HiddenState.Silent);
        }

        #endregion

        #region Game Context Menu Overlay Events

        private async void OnContextMenu_LaunchGame(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                GameContextMenuOverlayBase.Hide(HiddenState.Confirm);
                await ViewModel.LaunchGameAsync(ViewModel.ContextMenuTargetGame);
            }
        }

        private void OnContextMenu_SetEmulator(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                var game = ViewModel.ContextMenuTargetGame;
                ShowGameEmulatorOverlay(game);
            }
        }

        private static async Task<(ISaveBackupService? service, IEmulatorCommand command)> CreateBackupServiceAsync(GameMetadata game)
        {
            var command = RetroArchSaveBackupServiceBase.GetEmulatorCommand(game.PlatformId);

            var oauthService = GoogleOAuthFactory.Create();
            var driveService = new GoogleDriveService();

            var authenticated = await oauthService.TryAuthenticateFromStoredTokenAsync();

            if (!authenticated)
                authenticated = await oauthService.AuthenticateAsync();

            if (!authenticated)
            {
                await DialogService.Instance.ShowError("Google 인증에 실패했습니다.");
                return (null, command);
            }

            driveService.SetAccessToken(oauthService.AccessToken);
            var factory = SaveBackupServiceFactoryProvider.Create(driveService);

            if (!factory.IsSupported(command))
            {
                var statusMessage = factory.GetStatusMessage(command);

                await DialogService.Instance.ShowError(statusMessage);
                return (null, command);
            }

            var service = factory.CreateService(command);
            if (service == null)
            {
                await DialogService.Instance.ShowError("백업 서비스를 생성할 수 없습니다.");
                return (null, command);
            }

            return (service, command);
        }

        private static async Task HandleBackupErrorAsync(Exception ex)
        {
            switch (ex)
            {
                case NotSupportedException:
                    await DialogService.Instance.ShowError($"지원하지 않는 에뮬레이터입니다.\n{ex.Message}");
                    break;
                case UnauthorizedAccessException:
                    await DialogService.Instance.ShowError("세이브 파일에 접근할 수 없습니다.\n파일 권한을 확인해주세요.");
                    break;
                case IOException:
                    await DialogService.Instance.ShowError($"파일 읽기/쓰기 오류가 발생했습니다.\n{ex.Message}");
                    break;
                case HttpRequestException:
                    await DialogService.Instance.ShowError("네트워크 오류가 발생했습니다.\n인터넷 연결을 확인해주세요.");
                    break;
                default:
                    await DialogService.Instance.ShowError(ex.Message);
                    break;
            }
        }

        private async void OnContextMenu_SetSaveFileUpload(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame == null) return;

            var menuFocusSnapshot = FocusHelper.CreateSnapshot();

            try
            {
                var (service, command) = await CreateBackupServiceAsync(ViewModel.ContextMenuTargetGame);

                if (service == null) return;

                this.ShowLoading("백업 중...");

                var settings = SettingsService.LoadSettings();
                var mode = settings.SaveBackupMode;

                if (await service.BackupSaveAsync(ViewModel.ContextMenuTargetGame, mode))
                    await DialogService.Instance.ShowSuccess("백업 완료!");
                else
                    await DialogService.Instance.ShowError("백업에 실패했습니다.\n세이브 파일을 찾을 수 없거나 업로드 중 오류가 발생했습니다.");
            }
            catch (Exception ex)
            {
                await HandleBackupErrorAsync(ex);
            }
            finally
            {
                this.HideLoading();
                await Task.Delay(50);
                FocusHelper.SetFocus(menuFocusSnapshot?.SavedElement);
            }
        }

        private async void OnContextMenu_SetSaveFileDownload(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame == null) return;

            var menuFocusSnapshot = FocusHelper.CreateSnapshot();

            try
            {
                var (service, command) = await CreateBackupServiceAsync(ViewModel.ContextMenuTargetGame);

                if (service == null) return;

                this.ShowLoading("백업 목록 불러오는 중...");
                var backups = await service.GetBackupListAsync(ViewModel.ContextMenuTargetGame);
                this.HideLoading();

                if (backups == null || backups.Count == 0)
                {
                    await DialogService.Instance.ShowError("백업 파일을 찾을 수 없습니다.");
                    return;
                }

                var selectedFileId = await BackupListOverlayBase.ShowBackupListAsync(
                    ViewModel.ContextMenuTargetGame.GetTitle(),
                    backups
                );

                if (string.IsNullOrEmpty(selectedFileId)) return;

                this.ShowLoading("복원 중...");

                if (await service.RestoreSaveAsync(ViewModel.ContextMenuTargetGame, selectedFileId))
                    await DialogService.Instance.ShowSuccess("복원 완료!");
                else
                    await DialogService.Instance.ShowError("복원에 실패했습니다.\n다운로드 중 오류가 발생했습니다.");
            }
            catch (Exception ex)
            {
                await HandleBackupErrorAsync(ex);
            }
            finally
            {
                this.HideLoading();
                await Task.Delay(50);
                FocusHelper.SetFocus(menuFocusSnapshot?.SavedElement);
            }
        }

        private async void OnContextMenu_ToggleFavorite(object? sender, EventArgs e)
        {
            await WavSounds.Click();
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                ViewModel.ToggleFavorite(ViewModel.ContextMenuTargetGame);
                ViewModel.RequestSave();
            }
        }

        private void OnContextMenu_RenameGame(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null) ShowRenameOverlay(ViewModel.ContextMenuTargetGame);
        }

        private void OnContextMenu_ChangeGenre(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                ViewModel.SelectedGame = ViewModel.ContextMenuTargetGame;
                ShowGameGenreOverlay(ViewModel.ContextMenuTargetGame);
            }
        }

        private async void OnContextMenu_ToggleKorean(object? sender, EventArgs e)
        {
            await WavSounds.Click();
            if (ViewModel?.ContextMenuTargetGame != null)
                ViewModel.RequestSave();
        }

        private async void OnContextMenu_SetLogoImage(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                await WavSounds.Click();
                var game = ViewModel.ContextMenuTargetGame;
                ViewModel.SetLogoImage(game);
                OnContextMenu_ImageSet();
            }
        }

        private async void OnContextMenu_SetCoverImage(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                await WavSounds.Click();
                var game = ViewModel.ContextMenuTargetGame;
                ViewModel.SetCoverImage(game);
                OnContextMenu_ImageSet();
            }
        }

        private async void OnContextMenu_SetGameVideo(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                await WavSounds.Click();
                var game = ViewModel.ContextMenuTargetGame;
                ViewModel.SetGameVideo(game);
                OnContextMenu_VideoSet();
            }
        }

        private async void OnContextMenu_ToggleIgnore(object? sender, EventArgs e)
        {
            await WavSounds.Click();
            if (ViewModel?.ContextMenuTargetGame != null)
                ViewModel.RequestSave();
        }

        private void OnContextMenu_ScrapStarting(object? sender, EventArgs e)
        {
            ViewModel.IsDownloadingMedia = true;
            ScreenSaverManager.Instance.PauseScreenSaver();
            OnScrapStarting();
        }

        private void OnContextMenu_ScrapEnded(object? sender, EventArgs e)
        {
            ViewModel.IsDownloadingMedia = false;
            ScreenSaverManager.Instance.ResumeScreenSaver();

            OnScrapEnded();
        }

        #endregion

        #region Game Emulator Overlay Events

        private void OnGameEmulator_DefaultSelected(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                ViewModel.ContextMenuTargetGame.EmulatorId = null;
                ViewModel.RequestSave();
                GameEmulatorOverlayBase.Hide(HiddenState.Confirm);
                GameContextMenuOverlayBase.Hide(HiddenState.Silent);
            }
        }

        private void OnGameEmulator_EmulatorSelected(object? sender, EmulatorInfo emulator)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                ViewModel.ContextMenuTargetGame.EmulatorId = emulator.Id;
                ViewModel.RequestSave();
                GameEmulatorOverlayBase.Hide(HiddenState.Confirm);
                GameContextMenuOverlayBase.Hide(HiddenState.Silent);
            }
        }

        #endregion

        #region Game Rename Overlay Events

        private void OnGameRename_Save(object? sender, EventArgs e)
        {
            if (ViewModel?.ContextMenuTargetGame != null)
            {
                var newTitle = GameRenameOverlayBase.Text?.Trim();
                if (!string.IsNullOrEmpty(newTitle))
                {
                    ViewModel.ContextMenuTargetGame.Title = newTitle;
                    ViewModel.RequestSave();
                }
            }
            GameRenameOverlayBase.Hide(HiddenState.Confirm);
        }

        #endregion

        #region Game Genre Overlay Events

        private void OnGameGenre_Selected(object? sender, string genre)
        {
            if (ViewModel?.SelectedGame != null)
            {
                ViewModel.SelectedGame.Genre = string.IsNullOrEmpty(genre) ? null : genre;
                ViewModel.RequestSave();
                GameGenreOverlayBase.Hide(HiddenState.Silent);
                GameContextMenuOverlayBase.Hide(HiddenState.Silent);
            }
        }

        #endregion

        #region Overlay Display Helpers

        private async void ShowGameEmulatorOverlay(GameMetadata game)
        {
            if (GameEmulatorOverlayBase == null || ViewModel == null) return;

            await WavSounds.Click();
            GameEmulatorOverlayBase.SetEmulators(game, ViewModel.GetAvailableEmulators());
            GameEmulatorOverlayBase.Show();
        }

        private async void ShowRenameOverlay(GameMetadata game)
        {
            if (GameRenameOverlayBase == null) return;

            OnRenameOverlayShowing();
            await WavSounds.Click();
            GameRenameOverlayBase.Text = game.GetTitle();
            GameRenameOverlayBase.Show();
        }

        private async void ShowGameGenreOverlay(GameMetadata game)
        {
            await WavSounds.Click();
            GameGenreOverlayBase.SetGenres(ViewModel.EditingGenres, game.Genre);
            GameGenreOverlayBase.Show();
        }

        #endregion

        #region Virtual Methods

        protected virtual void OnOverlayShowing(object? sender, EventArgs e) { }

        protected virtual void OnContextMenu_ImageSet() { }

        protected virtual void OnContextMenu_VideoSet() { }

        protected virtual void OnScrapStarting() { }

        protected virtual void OnScrapEnded() { }

        protected virtual void OnRenameOverlayShowing() { }

        protected virtual void OnOverlayHiddenCore(HiddenEventArgs e) { }

        #endregion
    }
}