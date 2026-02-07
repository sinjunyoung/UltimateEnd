using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Scraper.Helpers;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Extractor
{
    public class ExtractMetadataEnricher
    {
        private readonly string _imageDirectory;
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public event Action<Models.GameMetadata, ExtractorMetadata> MetadataExtracted;

        public ExtractMetadataEnricher(string platformId)
        {
            var factory = AppBaseFolderProviderFactory.Create.Invoke();
            _imageDirectory = Path.Combine(factory.GetPlatformsFolder(), platformId);
        }

        public async Task ExtractInBackground(string platformId, IEnumerable<Models.GameMetadata> games, int maxParallel = 2)
        {
            if (_isRunning) return;

            var systemId = PlatformInfoService.Instance.GetScreenScraperSystemId(platformId);
            bool isArcade = ScreenScraperSystemClassifier.IsArcadeSystem(systemId);
            bool canExtract = MetadataExtractorFactory.IsSupported(platformId);
            bool hasDbSupport = IsDbSupported(platformId);

            if (!isArcade && !canExtract && !hasDbSupport) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                try
                {
                    var gameList = games.Where(g => !g.HasCoverImage).ToList();
                    var total = gameList.Count;

                    if (total == 0) return;

                    var current = 0;

                    await Parallel.ForEachAsync(gameList, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = maxParallel,
                        CancellationToken = _cts.Token
                    }, async (game, ct) =>
                    {
                        try
                        {
                            await ProcessGame(platformId, game);

                            Interlocked.Increment(ref current);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error processing game {game.Title}: {ex.Message}");
                        }
                    });
                }
                finally
                {
                    _isRunning = false;
                }
            }, _cts.Token);
        }

        private async Task ProcessGame(string platformId, Models.GameMetadata game)
        {
            var romPath = game.GetRomFullPath();

            if (!File.Exists(romPath)) return;

            var titleId = Path.GetFileNameWithoutExtension(romPath);
            var imagePath = Path.Combine(_imageDirectory, titleId, "image.png");

            if (File.Exists(imagePath))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!game.HasCoverImage) game.CoverImagePath = imagePath;
                    if (!game.HasLogoImage) game.LogoImagePath = imagePath;
                });
                return;
            }

            try
            {
                ExtractorMetadata metadata;

                var systemId = PlatformInfoService.Instance.GetScreenScraperSystemId(platformId);
                bool isArcade = ScreenScraperSystemClassifier.IsArcadeSystem(systemId);

                if (isArcade)
                {
                    metadata = new ExtractorMetadata
                    {
                        Id = Path.GetFileNameWithoutExtension(romPath)
                    };
                }
                else if (MetadataExtractorFactory.IsSupported(platformId))
                {
                    var extractor = MetadataExtractorFactory.GetExtractor(platformId);
                    metadata = await extractor.Extract(romPath);

                    if (metadata == null) return;
                }
                else
                {
                    return;
                }

                if (!string.IsNullOrEmpty(metadata.Id))
                    EnrichFromDatabase(platformId, metadata);

                Debug.WriteLine($"{metadata.Id}/{metadata.Title}");

                if (metadata.Image != null)
                {
                    var titleDir = Path.Combine(_imageDirectory, titleId);
                    Directory.CreateDirectory(titleDir);

                    imagePath = Path.Combine(titleDir, "image.png");
                    await File.WriteAllBytesAsync(imagePath, metadata.Image);
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (!string.IsNullOrEmpty(metadata.Title) && (string.IsNullOrEmpty(game.Title) || game.Title == Path.GetFileNameWithoutExtension(game.RomFile)))
                        game.Title = metadata.Title;
                    if (!string.IsNullOrEmpty(metadata.Developer) && string.IsNullOrEmpty(game.Developer))
                        game.Developer = metadata.Developer;
                    if (!string.IsNullOrEmpty(metadata.Genre) && string.IsNullOrEmpty(game.Genre))
                        game.Genre = metadata.Genre;
                    if (!string.IsNullOrEmpty(metadata.Description) && string.IsNullOrEmpty(game.Description))
                        game.Description = metadata.Description;

                    if (metadata.HasKorean)
                        game.HasKorean = metadata.HasKorean;

                    if (File.Exists(imagePath))
                    {
                        if (!game.HasCoverImage) game.CoverImagePath = imagePath;
                        if (!game.HasLogoImage) game.LogoImagePath = imagePath;
                    }
                });

                MetadataExtracted?.Invoke(game, metadata);
            }
            catch { }
        }

        public async Task ForceExtract(string platformId, Models.GameMetadata game)
        {
            var romPath = game.GetRomFullPath();
            var titleId = Path.GetFileNameWithoutExtension(romPath);
            var titleDir = Path.Combine(_imageDirectory, titleId);

            if (Directory.Exists(titleDir))
                Directory.Delete(titleDir, true);

            await ProcessGame(platformId, game);
        }

        private static void EnrichFromDatabase(string platformId, ExtractorMetadata metadata)
        {
            if (!IsDbSupported(platformId)) return;

            try
            {
                var screenScraperSystemId = PlatformInfoService.Instance.GetScreenScraperSystemId(platformId);
                var game = GameRepository.Instance.GetGame((int)screenScraperSystemId, metadata.Id);

                if (game != null)
                {
                    metadata.Title = game.Name ?? game.NameEn;
                    metadata.Description = game.Description;
                    metadata.Developer = game.Developer;
                    metadata.HasKorean = game.Languages.Contains("KO");
                    metadata.Genre = ScreenScraperGenre.GetFirstGenreKorean(game.GenreId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB lookup failed: {ex.Message}");
            }
        }

        private static bool IsDbSupported(string platformId)
        {
            return platformId switch
            {
                "playstation2" => true,
                "playstationportable" => true,
                "nintendods" => true,
                "fbneo" => true,
                _ => false
            };
        }

        public void Cancel()
        {
            _cts?.Cancel();
            _isRunning = false;
        }

        public long GetCacheSizeMB()
        {
            if (!Directory.Exists(_imageDirectory)) return 0;

            long size = 0;
            var dirInfo = new DirectoryInfo(_imageDirectory);

            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                size += file.Length;

            return size / 1024 / 1024;
        }

        public void ClearCache()
        {
            if (Directory.Exists(_imageDirectory))
            {
                Directory.Delete(_imageDirectory, true);
                Directory.CreateDirectory(_imageDirectory);
            }
        }
    }
}