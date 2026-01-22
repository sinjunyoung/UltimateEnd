using System;
using System.Reactive.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public partial class GameListView
    {
        #region Abstract Overlay Properties Implementation

        protected override EmulatorSelectionOverlay EmulatorOverlayBase => EmulatorOverlay;

        protected override GameContextMenuOverlay GameContextMenuOverlayBase => GameContextMenuOverlay;

        protected override GameEmulatorSelectionOverlay GameEmulatorOverlayBase => GameEmulatorOverlay;

        protected override GameGenreOverlay GameGenreOverlayBase => GameGenreOverlay;

        protected override GameRenameOverlay GameRenameOverlayBase => GameRenameOverlay;

        protected override GenreFilterOverlay GenreFilterOverlayBase => GenreFilterOverlay;

        protected override SettingsMenuOverlay SettingsMenuOverlayBase => SettingsMenuOverlay;

        protected override PlaylistSelectionOverlay PlaylistSelectionOverlayBase => PlaylistOverlay;

        protected override FolderContextMenuOverlay FolderContextMenuOverlayBase => FolderContextMenuOverlay;

        #endregion        

        #region Video Container Management

        private bool VideoContainerVisible
        {
            get => VideoContainer.IsVisible;
            set
            {
                VideoContainer.IsVisible = value;
                if (value && ViewModel?.SelectedGame != null)
                    ViewModel?.PlayInitialVideoCommand.Execute(ViewModel.SelectedGame).Subscribe();
            }
        }

        #endregion

        #region Overlay Registration Override

        protected override void RegisterOverlays()
        {
            base.RegisterOverlays();

            _overlays.Add("DescriptionEditOverlay", DescriptionEditOverlay);


            SettingsMenuOverlay.ResetLayoutClicked += (sender, e) =>
            {
                var settings = SettingsService.LoadSettings();
                settings.ResetGameListViewLayout();
                LoadSplitterPosition();
                LoadVerticalSplitterPosition();
                SettingsService.SaveSettingsQuiet(settings);
                SettingsMenuOverlay.Hide(HiddenState.Confirm);
            };

            DescriptionEditOverlay.Showing += OnOverlayShowing;
            DescriptionEditOverlay.Hidden += OnDescriptionEditOverlay_Hidden;
            DescriptionEditOverlay.SaveRequested += OnDescriptionEdit_Save;
            DescriptionEditOverlay.Click += async (s, e) => await WavSounds.Click();
        }

        #endregion

        #region Overlay Hook Overrides

        protected override void OnOverlayShowing(object? sender, EventArgs e) => VideoContainerVisible = false;

        protected override void OnOverlayHiddenCore(HiddenEventArgs e)
        {
            VideoContainerVisible = true;

            if (ViewModel != null) ViewModel.IsEditingDescriptionOverlay = false;
        }

        protected override void OnRenameOverlayShowing() => VideoContainerVisible = false;

        protected override void OnContextMenu_ImageSet() => VideoContainerVisible = false;

        protected override void OnContextMenu_VideoSet() => VideoContainerVisible = false;

        protected override void OnScrapStarting() => ViewModel?.StopVideo();

        protected override void OnScrapEnded()
        {
            if (ViewModel?.ContextMenuTargetGame != null) ViewModel.PlayInitialVideoCommand.Execute(GameContextMenuOverlay.SelectedGame).Subscribe();
        }

        #endregion

        #region Description Edit Overlay Events

        private void OnDescriptionEditOverlay_Hidden(object? sender, HiddenEventArgs e)
        {
            switch (e.State)
            {
                case HiddenState.Close:
                case HiddenState.Cancel:
                    _ = WavSounds.Cancel();
                    break;
                case HiddenState.Confirm:
                    _ = WavSounds.OK();
                    break;
            }

            GameScrollViewerFocusLoaded();
            VideoContainerVisible = true;

            if (ViewModel != null) ViewModel.IsEditingDescriptionOverlay = false;
        }

        private void OnDescriptionEdit_Save(object? sender, EventArgs e)
        {
            if (ViewModel?.SelectedGame != null)
            {
                ViewModel.SelectedGame.Description = DescriptionEditOverlay.Text;
                ViewModel.RequestSave();
            }

            DescriptionEditOverlay.Hide(HiddenState.Confirm);
        }

        #endregion
    }
}