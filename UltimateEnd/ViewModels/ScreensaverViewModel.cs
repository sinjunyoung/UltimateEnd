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

        private List<GameMetadata> _videoFavorites = [];
        private List<GameMetadata> _videoNormal = [];
        private List<GameMetadata> _allFavorites = [];
        private List<GameMetadata> _allNormal = [];
        private bool _cacheBuilt = false;

        public event Action? NavigateToGame;
        public event Action? ExitScreensaver;

        #region Properties

        public GameMetadata? CurrentGame
        {
            get => _currentGame;
            private set
            {
                this.RaiseAndSetIfChanged(ref _currentGame, value);
                UpdatePlatformLogoImageAsync();
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
            _videoChangeTimer.Tick += (s, e) => SelectRandomGame();

            NavigateToGameCommand = ReactiveCommand.Create(NavigateToCurrentGame);
            ExitScreensaverCommand = ReactiveCommand.Create(Exit);

            UpdateClock();
        }

        public async Task<bool> InitializeAsync(List<Platform> platforms)
        {
            _platformsById.Clear();
            _mappingConfig = PlatformMappingService.Instance.LoadMapping();

            foreach (var platform in platforms)
                _platformsById[platform.Id] = platform;

            await Task.Run(() => BuildGameCache());

            int totalVideoGames = _videoFavorites.Count + _videoNormal.Count;

            if (totalVideoGames == 0)
                return false;

            SelectRandomGame();

            _videoChangeTimer.Start();

            return true;
        }

        private void BuildGameCache()
        {
            var allGames = AllGamesManager.Instance.GetAllGames();

            var estimatedSize = allGames.Count / 4;
            _videoFavorites = new List<GameMetadata>(estimatedSize);
            _videoNormal = new List<GameMetadata>(estimatedSize * 2);
            _allFavorites = new List<GameMetadata>(estimatedSize);
            _allNormal = new List<GameMetadata>(estimatedSize * 2);

            foreach (var game in allGames)
            {
                if (game.Ignore) continue;

                if (game.HasVideo)
                {
                    if (game.IsFavorite)
                        _videoFavorites.Add(game);
                    else
                        _videoNormal.Add(game);
                }

                if (game.IsFavorite)
                    _allFavorites.Add(game);
                else
                    _allNormal.Add(game);
            }

            _cacheBuilt = true;
        }

        private void SelectRandomGame()
        {
            if (!_cacheBuilt) return;

            int totalVideoGames = _videoFavorites.Count + _videoNormal.Count;
            if (totalVideoGames == 0) return;

            List<GameMetadata> targetList;

            if (_random.NextDouble() < 0.2 && _videoFavorites.Count > 0)
                targetList = _videoFavorites;
            else if (_videoNormal.Count > 0)
                targetList = _videoNormal;
            else
                targetList = _videoFavorites;

            if (targetList.Count > 0)
                CurrentGame = targetList[_random.Next(targetList.Count)];
        }

        private async void UpdatePlatformLogoImageAsync()
        {
            var oldImage = _platformLogoImage;
            _platformLogoImage = null;
            PlatformLogoImage = null;

            await Task.Delay(1);
            oldImage?.Dispose();

            if (_currentGame == null) return;

            await Task.Run(async () =>
            {
                try
                {
                    var mappedPlatformId = PlatformMappingService.Instance.GetMappedPlatformId(_currentGame.PlatformId);
                    var logoUri = ResourceHelper.GetLogoImage(mappedPlatformId ?? _currentGame.PlatformId);

                    if (logoUri != null)
                    {
                        var uri = new Uri(logoUri);
                        Bitmap? newBitmap = null;

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
                                    newBitmap = new Bitmap(memStream);
                                }
                            }
                        }
                        else
                        {
                            using var stream = AssetLoader.Open(uri);
                            newBitmap = new Bitmap(stream);
                        }

                        await Dispatcher.UIThread.InvokeAsync(() => PlatformLogoImage = newBitmap);
                    }
                }
                catch
                {
                    await Dispatcher.UIThread.InvokeAsync(() => PlatformLogoImage = null);
                }
            });
        }

        private string GetActualPlatformId(string gamePlatformId)
        {
            if (_mappingConfig?.FolderMappings == null) return gamePlatformId;

            var mappedId = PlatformMappingService.Instance.GetMappedPlatformId(gamePlatformId);

            return mappedId ?? gamePlatformId;
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
            if (_currentGame?.PlatformId == null) return (null, null);

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

            if (CurrentGame?.HasVideo == true) await VideoPlayerManager.Instance.PlayWithDelayAsync(CurrentGame);
        }

        public void Dispose()
        {
            _clockTimer?.Stop();
            _videoChangeTimer?.Stop();
            VideoPlayerManager.Instance?.Stop();

            _platformLogoImage?.Dispose();
            _platformLogoImage = null;

            _platformsById.Clear();

            _videoFavorites.Clear();
            _videoNormal.Clear();
            _allFavorites.Clear();
            _allNormal.Clear();

            CurrentGame = null;
        }
    }
}