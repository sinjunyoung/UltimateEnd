using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class FolderContextMenuOverlay : BaseOverlay
    {
        private FolderItem? _currentFolder;
        private string? _platformId;
        private string? _basePath;
        private int _selectedMenuIndex = 0;
        private const int MenuItemCount = 2;

        public override bool Visible => MainGrid?.IsVisible ?? false;

        public event EventHandler<string>? FolderRenamed;
        public event EventHandler<string>? FolderDeleted;
        public event EventHandler<(string folderName, bool ignore)>? FolderIgnoreChanged;

        public FolderContextMenuOverlay()
        {
            InitializeComponent();

            if (RenameTextBox != null)
                RenameTextBox.KeyDown += OnRenameTextBoxKeyDown;
        }

        protected override void MovePrevious()
        {
            _selectedMenuIndex = (_selectedMenuIndex - 1 + MenuItemCount) % MenuItemCount;
            UpdateMenuSelection();
        }

        protected override void MoveNext()
        {
            _selectedMenuIndex = (_selectedMenuIndex + 1) % MenuItemCount;
            UpdateMenuSelection();
        }

        protected override void SelectCurrent()
        {
            switch (_selectedMenuIndex)
            {
                case 0:
                    ShowRenameOverlay();
                    break;
                case 1:
                    ToggleIgnore();
                    break;
            }
        }

        private void UpdateMenuSelection()
        {
            var renameBorder = this.FindControl<Border>("RenameBorder");
            var ignoreBorder = this.FindControl<Border>("IgnoreBorder");

            if (renameBorder != null)
            {
                renameBorder.Background = _selectedMenuIndex == 0
                    ? this.FindResource("Background.Hover") as IBrush
                    : this.FindResource("Background.Secondary") as IBrush;
            }

            if (ignoreBorder != null)
            {
                ignoreBorder.Background = _selectedMenuIndex == 1
                    ? this.FindResource("Background.Hover") as IBrush
                    : this.FindResource("Background.Secondary") as IBrush;
            }
        }

        public void SetFolder(FolderItem folder, string platformId, string basePath)
        {
            _currentFolder = folder;
            _platformId = platformId;
            _basePath = Directory.GetParent(basePath).FullName;

            if (FolderNameText != null)
                FolderNameText.Text = folder.Name;
        }

        public override void Show()
        {
            if (_currentFolder == null) return;

            OnShowing(EventArgs.Empty);

            if (MainGrid != null)
                MainGrid.IsVisible = true;

            this.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            _selectedMenuIndex = 0;

            Dispatcher.UIThread.Post(() =>
            {
                UpdateMenuSelection();
                UpdateIgnoreToggle();
            }, DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            HideRenameOverlay();
            HideDeleteOverlay();

            if (MainGrid != null)
                MainGrid.IsVisible = false;

            this.IsVisible = false;
            _currentFolder = null;
            _platformId = null;
            _basePath = null;

            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnClose(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            Hide(HiddenState.Close);
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Hide(HiddenState.Cancel);

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) Hide(HiddenState.Close);
        }

        #region Rename

        private void OnRenameClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            ShowRenameOverlay();
        }

        private void ShowRenameOverlay()
        {
            if (RenameOverlay == null || RenameTextBox == null || _currentFolder == null) return;

            RenameTextBox.Text = _currentFolder.Name;
            RenameStatusText.Text = " ";
            RenameOverlay.IsVisible = true;

            Dispatcher.UIThread.Post(() =>
            {
                RenameTextBox.Focus();
                RenameTextBox.SelectAll();
            }, DispatcherPriority.Loaded);
        }

        private void HideRenameOverlay()
        {
            if (RenameOverlay != null)
            {
                RenameOverlay.IsVisible = false;
                Dispatcher.UIThread.Post(() => this.Focus(), DispatcherPriority.Loaded);
            }
        }

        private void OnRenameTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;
                ConfirmRename();
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                e.Handled = true;
                HideRenameOverlay();
            }
        }

        private void OnRenameOverlayClose(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            HideRenameOverlay();
        }

        private void OnRenameOverlayBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) HideRenameOverlay();
        }

        private void OnRenameCancel(object? sender, RoutedEventArgs e) => HideRenameOverlay();

        private void OnRenameConfirm(object? sender, RoutedEventArgs e) => ConfirmRename();

        private void ConfirmRename()
        {
            if (_currentFolder == null || string.IsNullOrEmpty(_basePath)) return;

            var newName = RenameTextBox?.Text?.Trim();

            if (string.IsNullOrEmpty(newName))
            {
                if (RenameStatusText != null) RenameStatusText.Text = "폴더 이름을 입력하세요";
                return;
            }

            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                if (RenameStatusText != null) RenameStatusText.Text = "폴더 이름에 사용할 수 없는 문자가 포함되어 있습니다";
                return;
            }

            try
            {
                var converter = PathConverterFactory.Create?.Invoke();
                var realBasePath = converter?.FriendlyPathToRealPath(_basePath) ?? _basePath;

                var oldPath = Path.Combine(realBasePath, _currentFolder.SubFolder!);
                var newPath = Path.Combine(realBasePath, newName);

                if (Directory.Exists(newPath))
                {
                    if (RenameStatusText != null) RenameStatusText.Text = $"'{newName}' 폴더가 이미 존재합니다";
                    return;
                }

                Directory.Move(oldPath, newPath);
                var games = GameMetadataManager.LoadGames(_platformId!);

                foreach (var game in games.Where(g => g.SubFolder == _currentFolder.SubFolder))
                {
                    AllGamesManager.Instance.UpdateGameKey(game, _currentFolder.SubFolder!, newName);

                    game.SubFolder = newName;
                    game.SetBasePath(newPath);

                    if (!string.IsNullOrEmpty(game.CoverImagePath))
                        game.CoverImagePath = UpdateMediaPath(game.CoverImagePath, _currentFolder.SubFolder!, newName);

                    if (!string.IsNullOrEmpty(game.LogoImagePath))
                        game.LogoImagePath = UpdateMediaPath(game.LogoImagePath, _currentFolder.SubFolder!, newName);

                    if (!string.IsNullOrEmpty(game.VideoPath))
                        game.VideoPath = UpdateMediaPath(game.VideoPath, _currentFolder.SubFolder!, newName);

                    game.RefreshMediaCache();
                }

                AllGamesManager.Instance.SavePlatformGames(_platformId!);

                FolderRenamed?.Invoke(this, newName);

                HideRenameOverlay();
                Hide(HiddenState.Close);
            }
            catch (Exception ex)
            {
                if (RenameStatusText != null) RenameStatusText.Text = $"오류: {ex.Message}";
            }
        }

        private static string UpdateMediaPath(string mediaPath, string oldFolderName, string newFolderName)
        {
            if (mediaPath.Contains(oldFolderName))
                return mediaPath.Replace($"/{oldFolderName}/", $"/{newFolderName}/") .Replace($"\\{oldFolderName}\\", $"\\{newFolderName}\\");

            return mediaPath;
        }

        #endregion

        #region Delete

        private void OnDeleteClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            ShowDeleteOverlay();
        }

        private void ShowDeleteOverlay()
        {
            if (DeleteOverlay == null || _currentFolder == null) return;

            DeleteMessageText.Text = $"'{_currentFolder.Name}' 폴더를 삭제하시겠습니까?";
            DeleteOverlay.IsVisible = true;
        }

        private void HideDeleteOverlay()
        {
            if (DeleteOverlay != null)
            {
                DeleteOverlay.IsVisible = false;
                Dispatcher.UIThread.Post(() => this.Focus(), DispatcherPriority.Loaded);
            }
        }

        private void OnDeleteOverlayBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) HideDeleteOverlay();
        }

        private void OnDeleteCancel(object? sender, RoutedEventArgs e) => HideDeleteOverlay();

        private void OnDeleteConfirm(object? sender, RoutedEventArgs e)
        {
            if (_currentFolder == null || string.IsNullOrEmpty(_basePath) || string.IsNullOrEmpty(_platformId)) return;

            try
            {
                var converter = PathConverterFactory.Create?.Invoke();
                var realBasePath = converter?.FriendlyPathToRealPath(_basePath) ?? _basePath;
                var folderPath = Path.Combine(realBasePath, _currentFolder.SubFolder!);

                var games = GameMetadataManager.LoadGames(_platformId!);
                var gamesToRemove = games.Where(g => g.SubFolder == _currentFolder.SubFolder).ToList();

                foreach (var game in gamesToRemove)
                    games.Remove(game);

                AllGamesManager.Instance.SavePlatformGames(_platformId);

                FolderDeleted?.Invoke(this, _currentFolder.SubFolder!);

                HideDeleteOverlay();
                Hide(HiddenState.Close);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete folder error: {ex.Message}");
            }
        }

        private void OnIgnoreClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            ToggleIgnore();
        }

        private void ToggleIgnore()
        {
            if (_currentFolder == null || string.IsNullOrEmpty(_platformId)) return;

            var newIgnoreState = !_currentFolder.Ignore;
            _currentFolder.Ignore = newIgnoreState;

            var games = GameMetadataManager.LoadGames(_platformId!);

            foreach (var game in games.Where(g => g.SubFolder == _currentFolder.SubFolder))
                game.Ignore = newIgnoreState;

            AllGamesManager.Instance.SavePlatformGames(_platformId);

            UpdateIgnoreToggle();

            FolderIgnoreChanged?.Invoke(this, (_currentFolder.SubFolder!, newIgnoreState));
        }

        private void UpdateIgnoreToggle()
        {
            if (_currentFolder == null || string.IsNullOrEmpty(_platformId)) return;

            var allGames = GameMetadataManager.LoadGames(_platformId!);
            var folderGames = allGames.Where(g => g.SubFolder == _currentFolder.SubFolder).ToList();

            bool isIgnored = folderGames.Count != 0 && folderGames.All(g => g.Ignore);

            _currentFolder.Ignore = isIgnored; 
            UpdateToggle(IgnoreToggle, IgnoreToggleThumb, isIgnored);
        }

        private static void UpdateToggle(Border? toggleBack, Border? toggle, bool value)
        {
            if (toggleBack == null || toggle == null) return;

            string resourceKey = value ? "Toggle.SelectionBackground" : "Toggle.Background";

            if (Avalonia.Application.Current != null && Avalonia.Application.Current.Resources.TryGetResource(resourceKey, Avalonia.Application.Current?.ActualThemeVariant, out object? resourceObj)) toggleBack.Background = resourceObj as IBrush;

            toggle.HorizontalAlignment = value ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        }


        #endregion
    }
}