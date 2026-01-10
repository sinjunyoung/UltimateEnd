using Avalonia;
using Avalonia.Platform.Storage;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using UltimateEnd.Coordinators;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Managers
{
    public class MediaAssetManager
    {
        private readonly VideoPlaybackCoordinator _videoCoordinator;
        public IStorageProvider? StorageProvider { get; set; }

        public ReactiveCommand<GameMetadata, Unit> SetLogoImageCommand { get; set; }
        public ReactiveCommand<GameMetadata, Unit> SetCoverImageCommand { get; set; }
        public ReactiveCommand<GameMetadata, Unit> SetGameVideoCommand { get; set; }

        public event Action<GameMetadata, string>? LogoImageChanged;
        public event Action<GameMetadata, string>? CoverImageChanged;
        public event Action<GameMetadata, string>? VideoChanged;
        public event Action<string>? PlatformImageChanged;

        public MediaAssetManager(VideoPlaybackCoordinator videoCoordinator)
        {
            _videoCoordinator = videoCoordinator;
            SetLogoImageCommand = ReactiveCommand.Create<GameMetadata>(SetLogoImage);
            SetCoverImageCommand = ReactiveCommand.Create<GameMetadata>(SetCoverImage);
            SetGameVideoCommand = ReactiveCommand.Create<GameMetadata>(SetGameVideo);
        }

        public async void SetLogoImage(GameMetadata game)
        {
            var path = await SelectImageFile(game);

            game.LogoImagePath = path;
            LogoImageChanged?.Invoke(game, path);
        }

        public async void SetCoverImage(GameMetadata game)
        {
            var path = await SelectImageFile(game);

            game.CoverImagePath = path;
            CoverImageChanged?.Invoke(game, path);
        }

        public async void SetGameVideo(GameMetadata game)
        {
            var path = await SelectVideoFile(game);

            game.VideoPath = path;
            VideoChanged?.Invoke(game, path);
        }

        private async Task<string?> SelectImageFile(GameMetadata game)
        {
            try
            {
                _videoCoordinator.Stop();
                string romPath = game.GetRomFullPath();

                var converter = PathConverterFactory.Create?.Invoke();
                var initialDirectory = Path.GetDirectoryName(converter?.FriendlyPathToRealPath(romPath) ?? romPath);

                var path = await DialogHelper.OpenFileAsync(initialDirectory, FilePickerFileTypes.ImageAll);
                return ConvertPath(path);
            }
            finally { }
        }

        private async Task<string?> SelectVideoFile(GameMetadata game)
        {
            try
            {
                _videoCoordinator.Stop();
                var videoFilter = new FilePickerFileType("MP4 및 MKV 비디오 파일")
                {
                    Patterns = ["*.mp4", "*.mkv"],
                    MimeTypes = ["video/mp4", "video/x-matroska"]
                };

                string romPath = game.GetRomFullPath();

                var converter = PathConverterFactory.Create?.Invoke();
                var initialDirectory = Path.GetDirectoryName(converter?.FriendlyPathToRealPath(romPath) ?? romPath);

                var path = await DialogHelper.OpenFileAsync(initialDirectory, videoFilter);
                return ConvertPath(path);
            }
            finally
            {
                await Task.CompletedTask;
            }
        }

        private static string? ConvertPath(string? path)
        {
            var converter = PathConverterFactory.Create?.Invoke();
            return converter?.UriToFriendlyPath(path) ?? path;
        }

        public async Task<string?> SetPlatformImageAsync()
        {
            try
            {
                _videoCoordinator.Stop();

                var path = await DialogHelper.OpenFileAsync(null, FilePickerFileTypes.ImageAll);
                path = ConvertPath(path);
                PlatformImageChanged?.Invoke(path);

                return path;
            }
            finally { }
        }
    }
}