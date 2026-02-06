using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Scraper.Helpers;
using UltimateEnd.Services;

namespace UltimateEnd.Extractor
{
    public class ExtractMetadataEnricher(string platformId)
    {
        private readonly ExtractMetadataCache _cache = new(platformId);
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public event Action<Models.GameMetadata, ExtractorMetadata> MetadataExtracted;

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

            var cached = await _cache.GetCachedMetadata(romPath);

            if (cached != null)
            {
                ApplyMetadataToGame(game, cached);
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

                System.Diagnostics.Debug.WriteLine($"{metadata.Id}/{metadata.Title}");

                await _cache.SaveMetadata(romPath, metadata);
                cached = await _cache.GetCachedMetadata(romPath);

                ApplyMetadataToGame(game, cached);
                MetadataExtracted?.Invoke(game, metadata);
            }
            catch { }
        }

        private static void ApplyMetadataToGame(Models.GameMetadata game, CachedMetadata cached)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!string.IsNullOrEmpty(cached.Title) && (string.IsNullOrEmpty(game.Title) || game.Title == Path.GetFileNameWithoutExtension(game.RomFile))) game.Title = cached.Title;
                if (!string.IsNullOrEmpty(cached.Developer) && string.IsNullOrEmpty(game.Developer)) game.Developer = cached.Developer;
                //if (!string.IsNullOrEmpty(cached.Genre) && string.IsNullOrEmpty(game.Genre)) game.Genre = cached.Genre;
                if (!string.IsNullOrEmpty(cached.Description) && string.IsNullOrEmpty(game.Description)) game.Description = cached.Description;
                if (!string.IsNullOrEmpty(cached.CoverImagePath) && !game.HasCoverImage) game.CoverImagePath = cached.CoverImagePath;
                if (!string.IsNullOrEmpty(cached.LogoImagePath) && !game.HasLogoImage) game.LogoImagePath = cached.LogoImagePath;
            });
        }

        public async Task ForceExtract(string platformId, Models.GameMetadata game)
        {
            var romPath = game.GetRomFullPath();
            _cache.DeleteCache(romPath);
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

        public long GetCacheSizeMB() => _cache.GetCacheSize();

        public void ClearCache() => _cache.ClearAllCache();
    }
}