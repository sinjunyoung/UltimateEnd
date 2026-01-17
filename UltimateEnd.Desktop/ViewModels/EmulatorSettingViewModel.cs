using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using UltimateEnd.Desktop.Models;
using UltimateEnd.Desktop.Services;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Desktop.ViewModels
{
    public class EmulatorSettingViewModel : UltimateEnd.ViewModels.EmulatorSettingViewModelBase
    {
        private IStorageProvider _storageProvider;

        public ICommand BrowseExecutableCommand { get; }
        public ICommand BrowseWorkingDirCommand { get; }
        public ICommand BrowsePrelaunchScriptCommand { get; }
        public ICommand BrowsePostlaunchScriptCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public EmulatorSettingViewModel()
        {
            BrowseExecutableCommand = ReactiveCommand.CreateFromTask(BrowseExecutableAsync);
            BrowseWorkingDirCommand = ReactiveCommand.CreateFromTask(BrowseWorkingDirAsync);
            BrowsePrelaunchScriptCommand = ReactiveCommand.CreateFromTask(BrowsePrelaunchScriptAsync);
            BrowsePostlaunchScriptCommand = ReactiveCommand.CreateFromTask(BrowsePostlaunchScriptAsync);
            SaveCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                Save();
                GoBack();
                await WavSounds.OK();
            });
            GoBackCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                GoBack();
                await WavSounds.Cancel();
            });
            ClearSearchCommand = ReactiveCommand.Create(() => SearchText = string.Empty);
        }

        public void SetStorageProvider(IStorageProvider storageProvider) => _storageProvider = storageProvider;

        protected override ICommandConfigService GetConfigService() => new CommandConfigService();

        protected override IEmulatorCommand CreateNewCommand()
        {
            return new Command
            {
                Id = Guid.NewGuid().ToString(),
                Name = "신규 에뮬레이터",
                IsRetroArch = false,
                SupportedPlatforms = [],
                LaunchCommand = string.Empty
            };
        }

        protected override IEmulatorCommand CreateDuplicateCommand(IEmulatorCommand original)
        {
            return new Command
            {
                Id = Guid.NewGuid().ToString(),
                Name = original.Name + " (복사본)",
                IsRetroArch = original.IsRetroArch,
                CoreName = original.CoreName,
                SupportedPlatforms = new(original.SupportedPlatforms),
                LaunchCommand = original.LaunchCommand,
                WorkingDirectory = (original as Command)?.WorkingDirectory,
                PrelaunchScript = (original as Command)?.PrelaunchScript,
                PostlaunchScript = (original as Command)?.PostlaunchScript
            };
        }

        protected override void OnPlatformsChanged(List<string> platforms)
        {
            if (SelectedCommand is Command cmd)
                cmd.SupportedPlatforms = platforms;
        }

        public override Bitmap LoadPlatformImage(string platformId)
        {
            try
            {
                var uri = new Uri(ResourceHelper.GetPlatformImage(platformId));

                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);

                    return new Bitmap(stream);
                }
            }
            catch { }

            return null!;
        }

        private async Task BrowseExecutableAsync()
        {
            if (_storageProvider == null || SelectedCommand == null)
                return;

            var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "실행 파일 선택",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("실행 파일")
                    {
                        Patterns = ["*.exe"]
                    }
                ]
            });

            if (files.Count > 0 && SelectedCommand is Command cmd)
                cmd.Executable = files[0].Path.LocalPath;
        }

        private async Task BrowseWorkingDirAsync()
        {
            if (_storageProvider == null || SelectedCommand == null)
                return;

            var folders = await _storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "작업 디렉토리 선택",
                AllowMultiple = false
            });

            if (folders.Count > 0 && SelectedCommand is Command cmd)
                cmd.WorkingDirectory = folders[0].Path.LocalPath;
        }

        private async Task BrowsePrelaunchScriptAsync()
        {
            if (_storageProvider == null || SelectedCommand == null)
                return;

            var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Pre-launch 스크립트 선택",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("스크립트 파일")
                    {
                        Patterns = ["*.bat", "*.cmd", "*.ps1", "*.exe", "*.vbs"]
                    },
                    new FilePickerFileType("모든 파일")
                    {
                        Patterns = ["*.*"]
                    }
                ]
            });

            if (files.Count > 0 && SelectedCommand is Command cmd)
                cmd.PrelaunchScript = files[0].Path.LocalPath;
        }

        private async Task BrowsePostlaunchScriptAsync()
        {
            if (_storageProvider == null || SelectedCommand == null)
                return;

            var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Post-launch 스크립트 선택",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("스크립트 파일")
                    {
                        Patterns = ["*.bat", "*.cmd", "*.ps1", "*.exe", "*.vbs"]
                    },
                    new FilePickerFileType("모든 파일")
                    {
                        Patterns = ["*.*"]
                    }
                ]
            });

            if (files.Count > 0 && SelectedCommand is Command cmd)
                cmd.PostlaunchScript = files[0].Path.LocalPath;
        }
    }
}