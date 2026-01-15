using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Views
{
    public partial class RomSettingView : UserControl
    {
        public RomSettingView()
        {
            InitializeComponent();
            this.AttachedToVisualTree += OnAttachedToVisualTree;
            this.DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            Focus();
            if (DataContext is RomSettingViewModel vm)
                await vm.InitializeAsync();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        }

        protected async override void OnKeyDown(KeyEventArgs e)
        {
            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB) || e.Key == Key.Back)
            {
                if (DataContext is RomSettingViewModel vm)
                {
                    await WavSounds.Cancel();
                    vm.GoBack();
                }
                e.Handled = true;
                return;
            }

            if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                if (DataContext is RomSettingViewModel vm)
                {
                    await WavSounds.OK();
                    await vm.SaveSettingsAsync();
                    vm.GoBack();
                }
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private void OnAddBasePathClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is RomSettingViewModel vm)
                vm.AddBasePath();
        }

        private async void OnBrowseBasePathClick(object? sender, RoutedEventArgs e)
        {
            var picker = FolderPickerFactory.Create?.Invoke();

            if (picker != null && DataContext is RomSettingViewModel vm)
            {
                RomsBasePathItem? targetItem = null;

                if (sender is Button button && button.CommandParameter is RomsBasePathItem item)
                    targetItem = item;

                var path = await picker.PickFolderAsync("롬 폴더 선택");

                if (!string.IsNullOrEmpty(path))
                {
                    var converter = PathConverterFactory.Create?.Invoke();
                    var realPath = converter?.UriToFriendlyPath(path) ?? path;
                    var friendlyPath = converter?.RealPathToFriendlyPath(realPath) ?? realPath;

                    var normalizedNewPath = NormalizePath(friendlyPath);
                    var isDuplicate = vm.RomsBasePaths
                        .Where(bp => bp != targetItem)
                        .Any(bp => !string.IsNullOrEmpty(bp.Path) &&
                                   NormalizePath(bp.Path) == normalizedNewPath);

                    if (isDuplicate)
                    {
                        await DialogService.Instance.ShowWarning("이미 추가된 경로입니다.");
                        return;
                    }

                    vm.SetBasePath(friendlyPath, targetItem);
                }
            }
        }

        private void OnRemoveBasePathClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.CommandParameter is RomsBasePathItem item &&
                DataContext is RomSettingViewModel vm)
            {
                vm.RemoveBasePath(item);
            }
        }

        private void OnDeletePlatformClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button &&
                button.DataContext is PlatformNameSetting platform &&
                DataContext is RomSettingViewModel vm)
            {
                vm.DeletePlatform(platform);
            }
        }

        private async void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is RomSettingViewModel vm)
            {
                await vm.SaveSettingsAsync();
                vm.GoBack();
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is RomSettingViewModel vm)
                vm.GoBack();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            try
            {               
                return System.IO.Path.GetFullPath(path)
                    .TrimEnd('/', '\\')
                    .ToLowerInvariant();
            }
            catch
            {
                return path.TrimEnd('/', '\\').ToLowerInvariant();
            }
        }
    }
}