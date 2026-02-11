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

        public ExtractMetadataEnricher(string platformId)
        {
            var factory = AppBaseFolderProviderFactory.Create.Invoke();
            _imageDirectory = Path.Combine(factory.GetPlatformsFolder(), platformId);
        }

        public async Task<bool> ExtractInBackground(string platformId, IEnumerable<Models.GameMetadata> games)
        {
            if (_isRunning) return false;

            var systemId = PlatformInfoService.Instance.GetScreenScraperSystemId(platformId);
            bool isArcade = ScreenScraperSystemClassifier.IsArcadeSystem(systemId);
            bool canExtract = MetadataExtractorFactory.IsSupported(platformId);
            bool hasDbSupport = IsDbSupported(platformId);

            if (!isArcade && !canExtract && !hasDbSupport) return false;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            bool hasUpdates = false;

            await Task.Run(async () =>
            {
                try
                {
                    var gameList = games.Where(g => !g.HasCoverImage).ToList();
                    var total = gameList.Count;

                    if (total == 0) return;

                    foreach (var game in gameList)
                    {
                        if (_cts.Token.IsCancellationRequested) break;

                        try
                        {
                            bool updated = await ProcessGame(platformId, game);
                            if (updated) hasUpdates = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing game {game.Title}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    _isRunning = false;
                }
            }, _cts.Token);

            return hasUpdates;
        }

        private async Task<bool> ProcessGame(string platformId, Models.GameMetadata game)
        {
            var romPath = game.GetRomFullPath();

            if (!File.Exists(romPath)) return false;

            var titleId = Path.GetFileNameWithoutExtension(romPath);
            var imagePath = Path.Combine(_imageDirectory, titleId, "image.png");

            if (File.Exists(imagePath))
            {
                bool updated = false;

                if (!game.HasCoverImage)
                {
                    game.CoverImagePath = imagePath;
                    updated = true;
                }

                if (!game.HasLogoImage)
                {
                    game.LogoImagePath = imagePath;
                    updated = true;
                }

                return updated;
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

                    if (metadata == null) return false;
                }
                else
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(metadata.Id))
                    EnrichFromDatabase(platformId, metadata);

                Debug.WriteLine($"{metadata.Id}/{metadata.Title}");

                bool updated = false;

                if (metadata.Image != null)
                {
                    var titleDir = Path.Combine(_imageDirectory, titleId);
                    Directory.CreateDirectory(titleDir);

                    imagePath = Path.Combine(titleDir, "image.png");
                    await File.WriteAllBytesAsync(imagePath, metadata.Image);

                    metadata.Image = null;
                }

                if (!string.IsNullOrEmpty(metadata.Title) && (string.IsNullOrEmpty(game.Title) || game.Title == Path.GetFileNameWithoutExtension(game.RomFile)))
                {
                    game.Title = metadata.Title;
                    updated = true;
                }

                if (!string.IsNullOrEmpty(metadata.Developer) && string.IsNullOrEmpty(game.Developer))
                {
                    game.Developer = metadata.Developer;
                    updated = true;
                }

                if (!string.IsNullOrEmpty(metadata.Genre) && string.IsNullOrEmpty(game.Genre))
                {
                    game.Genre = metadata.Genre;
                    updated = true;
                }

                if (!string.IsNullOrEmpty(metadata.Description) && string.IsNullOrEmpty(game.Description))
                {
                    game.Description = metadata.Description;
                    updated = true;
                }

                if (metadata.HasKorean)
                {
                    game.HasKorean = metadata.HasKorean;
                    updated = true;
                }

                if (File.Exists(imagePath))
                {
                    if (!game.HasCoverImage)
                    {
                        game.CoverImagePath = imagePath;
                        updated = true;
                    }

                    if (!game.HasLogoImage)
                    {
                        game.LogoImagePath = imagePath;
                        updated = true;
                    }
                }

                return updated;
            }
            catch
            {
                return false;
            }
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
                    metadata.HasKorean = game.Languages.Contains("KO", StringComparison.InvariantCultureIgnoreCase);
                    metadata.Genre = ScreenScraperGenre.GetFirstGenreKorean(game.GenreId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DB lookup failed: {ex.Message}");
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
    }
}