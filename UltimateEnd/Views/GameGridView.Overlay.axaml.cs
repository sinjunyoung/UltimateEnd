using Avalonia.Controls;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public partial class GameGridView
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
    }
}