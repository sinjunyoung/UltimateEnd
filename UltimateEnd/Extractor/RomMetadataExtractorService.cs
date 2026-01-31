using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Models;

namespace UltimateEnd.Extractor
{
    public class RomMetadataExtractorService(string platformId)
    {
        private readonly RomMetadataCache _cache = new(platformId);
        private CancellationTokenSource _cts;
        private bool _isRunning;

        public event Action<GameMetadata, ExtractedMetadata> MetadataExtracted;

        public async Task ExtractInBackground(string platformId, IEnumerable<GameMetadata> games, int maxParallel = 2)
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            await Task.Run(async () =>
            {
                try
                {
                    if (!MetadataExtractorFactory.IsSupported(platformId)) return;

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

        private async Task ProcessGame(string platformId, GameMetadata game)
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
                var extractor = MetadataExtractorFactory.GetExtractor(platformId);
                var metadata = await extractor.Extract(romPath);

                if (metadata == null || string.IsNullOrEmpty(metadata.Title)) return;

                await _cache.SaveMetadata(romPath, metadata);
                cached = await _cache.GetCachedMetadata(romPath);

                ApplyMetadataToGame(game, cached);
                MetadataExtracted?.Invoke(game, metadata);
            }
            catch { }
        }

        private static void ApplyMetadataToGame(GameMetadata game, CachedMetadata cached)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!string.IsNullOrEmpty(cached.Title) && (string.IsNullOrEmpty(game.Title) || game.Title == Path.GetFileNameWithoutExtension(game.RomFile))) game.Title = cached.Title;
                if (!string.IsNullOrEmpty(cached.Developer) && string.IsNullOrEmpty(game.Developer)) game.Developer = cached.Developer;
                if (!string.IsNullOrEmpty(cached.CoverImagePath) && !game.HasCoverImage) game.CoverImagePath = cached.CoverImagePath;
                if (!string.IsNullOrEmpty(cached.LogoImagePath) && !game.HasLogoImage) game.LogoImagePath = cached.LogoImagePath;
            });
        }

        public async Task ForceExtract(string platformId, GameMetadata game)
        {
            var romPath = game.GetRomFullPath();
            _cache.DeleteCache(romPath);
            await ProcessGame(platformId, game);
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