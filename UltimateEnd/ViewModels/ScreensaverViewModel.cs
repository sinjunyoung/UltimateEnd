using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using ReactiveUI;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.ViewModels
{
    public class ScreensaverViewModel : ViewModelBase, IDisposable
    {
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _videoChangeTimer;
        private readonly Dictionary<string, Platform> _platformsById = [];
        private PlatformMappingConfig? _mappingConfig;

        private GameMetadata? _currentGame;
        private string _currentTime = string.Empty;
        private string _currentDate = string.Empty;
        private Bitmap? _platformLogoImage;

        private readonly Random _random = new();

        public event Action? NavigateToGame;
        public event Action? ExitScreensaver;

        #region Properties

        public GameMetadata? CurrentGame
        {
            get => _currentGame;
            private set
            {
                this.RaiseAndSetIfChanged(ref _currentGame, value);
                UpdatePlatformLogoImage();
                this.RaisePropertyChanged(nameof(GameTitle));
                this.RaisePropertyChanged(nameof(HasVideo));
            }
        }

        public Bitmap? PlatformLogoImage
        {
            get => _platformLogoImage;
            private set => this.RaiseAndSetIfChanged(ref _platformLogoImage, value);
        }

        public string GameTitle => _currentGame?.DisplayTitle ?? string.Empty;

        public bool HasVideo => _currentGame?.HasVideo ?? false;

        public string CurrentTime
        {
            get => _currentTime;
            private set => this.RaiseAndSetIfChanged(ref _currentTime, value);
        }

        public string CurrentDate
        {
            get => _currentDate;
            private set => this.RaiseAndSetIfChanged(ref _currentDate, value);
        }

        public static object? MediaPlayer => VideoPlayerManager.Instance.PlayerInstance;

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> NavigateToGameCommand { get; }
        public ReactiveCommand<Unit, Unit> ExitScreensaverCommand { get; }

        #endregion

        public ScreensaverViewModel()
        {
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();

            _videoChangeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _videoChangeTimer.Tick += async (s, e) => await SelectRandomGame();

            NavigateToGameCommand = ReactiveCommand.Create(NavigateToCurrentGame);
            ExitScreensaverCommand = ReactiveCommand.Create(Exit);

            UpdateClock();
        }

        public async Task InitializeAsync(List<Platform> platforms)
        {
            _platformsById.Clear();

            _mappingConfig = PlatformMappingService.Instance.LoadMapping();

            foreach (var platform in platforms)
                _platformsById[platform.Id] = platform;

            await SelectRandomGame();

            _videoChangeTimer.Start();
        }

        private void UpdatePlatformLogoImage()
        {
            _platformLogoImage?.Dispose();
            _platformLogoImage = null;

            if (_currentGame == null)
            {
                PlatformLogoImage = null;

                return;
            }

            try
            {
                var mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(_currentGame.PlatformId);
                var logoUri = ResourceHelper.GetLogoImage(mappedPlatformId ?? _currentGame.PlatformId);

                if (logoUri != null)
                {
                    var uri = new Uri(logoUri);

                    if (logoUri.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    {
                        var svg = SvgSource.Load(logoUri, uri);

                        if (svg?.Picture != null)
                        {
                            var bounds = svg.Picture.CullRect;

                            if (bounds.Width > 0 && bounds.Height > 0)
                            {
                                using var bitmap = new SKBitmap((int)bounds.Width, (int)bounds.Height);
                                using (var canvas = new SKCanvas(bitmap))
                                {
                                    canvas.Clear(SKColors.Transparent);
                                    canvas.DrawPicture(svg.Picture);
                                    canvas.Flush();
                                }

                                using var image = SKImage.FromBitmap(bitmap);
                                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                                using var memStream = new MemoryStream(data.ToArray());
                                PlatformLogoImage = new Bitmap(memStream);
                            }
                            else
                                PlatformLogoImage = null;
                        }
                        else
                            PlatformLogoImage = null;
                    }
                    else
                    {
                        using var stream = AssetLoader.Open(uri);
                        PlatformLogoImage = new Bitmap(stream);
                    }
                }
                else
                    PlatformLogoImage = null;
            }
            catch
            {
                PlatformLogoImage = null;
            }
        }

        private string GetActualPlatformId(string gamePlatformId)
        {
            if (_mappingConfig?.FolderMappings == null)
                return gamePlatformId;

            var mappedId = PlatformMappingService.Instance.GetMappedPlatformId(gamePlatformId);

            return mappedId ?? gamePlatformId;
        }

        private Task SelectRandomGame()
        {
            var allGames = AllGamesManager.Instance.GetAllGames()
                .Where(g => !g.Ignore)
                .ToList();

            if (allGames.Count == 0) return Task.CompletedTask;

            var gamesWithVideo = allGames.Where(g => g.HasVideo).ToList();
            var candidates = gamesWithVideo.Count > 0 ? gamesWithVideo : allGames;

            if (candidates.Count == 0) return Task.CompletedTask;

            var favorites = candidates.Where(g => g.IsFavorite).ToList();
            var nonFavorites = candidates.Where(g => !g.IsFavorite).ToList();

            GameMetadata newGame;

            if (favorites.Count > 0 && nonFavorites.Count > 0)
            {
                if (_random.NextDouble() < 0.2)
                    newGame = favorites[_random.Next(favorites.Count)];
                else
                    newGame = nonFavorites[_random.Next(nonFavorites.Count)];
            }
            else if (favorites.Count > 0)
                newGame = favorites[_random.Next(favorites.Count)];
            else if (nonFavorites.Count > 0)
                newGame = nonFavorites[_random.Next(nonFavorites.Count)];
            else
                return Task.CompletedTask;

            CurrentGame = newGame;
            return Task.CompletedTask;
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            CurrentTime = now.ToString("HH:mm:ss");
            CurrentDate = now.ToString("yyyy년 MM월 dd일 dddd");
        }

        private void NavigateToCurrentGame()
        {
            VideoPlayerManager.Instance.Stop();
            _videoChangeTimer.Stop();
            NavigateToGame?.Invoke();
        }

        private void Exit()
        {
            VideoPlayerManager.Instance.Stop();
            _videoChangeTimer.Stop();
            ExitScreensaver?.Invoke();
        }

        public (Platform? platform, GameMetadata? game) GetCurrentSelection()
        {
            if (_currentGame?.PlatformId == null)
                return (null, null);

            var actualPlatformId = GetActualPlatformId(_currentGame.PlatformId);
            _platformsById.TryGetValue(actualPlatformId, out var platform);
            return (platform, _currentGame);
        }

        public void Pause()
        {
            _clockTimer?.Stop();
            _videoChangeTimer?.Stop();
            VideoPlayerManager.Instance?.Stop();
        }

        public async Task Resume()
        {
            _clockTimer?.Start();
            _videoChangeTimer?.Start();

            if (CurrentGame?.HasVideo == true)
                await VideoPlayerManager.Instance.PlayWithDelayAsync(CurrentGame);
        }

        public void Dispose()
        {
            _clockTimer?.Stop();
            _videoChangeTimer?.Stop();
            VideoPlayerManager.Instance?.Stop();

            _platformLogoImage?.Dispose();
            _platformLogoImage = null;

            _platformsById.Clear();
            CurrentGame = null;
        }
    }
}