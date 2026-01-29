using Avalonia.Threading;
using ExCSS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
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

        public event Action<int, int> ProgressChanged;
        public event Action<GameMetadata, ExtractedMetadata> MetadataExtracted;
        public event Action Completed;

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

                    var gameList = games.ToList();
                    var total = gameList.Count;
                    var current = 0;

                    var semaphore = new SemaphoreSlim(maxParallel);
                    var tasks = gameList.Select(async game =>
                    {
                        if (_cts.Token.IsCancellationRequested) return;

                        if(game.HasCoverImage) return;

                        await semaphore.WaitAsync(_cts.Token);

                        try
                        {
                            await ProcessGame(platformId, game);

                            Interlocked.Increment(ref current);
                            ProgressChanged?.Invoke(current, total);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });

                    await Task.WhenAll(tasks);
                }
                finally
                {
                    _isRunning = false;
                    Completed?.Invoke();
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
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!string.IsNullOrEmpty(cached.Title)) game.Title = cached.Title;
                    if (!string.IsNullOrEmpty(cached.CoverImagePath)) game.CoverImagePath = cached.CoverImagePath;
                    if(!string.IsNullOrEmpty(cached.LogoImagePath)) game.LogoImagePath = cached.LogoImagePath;
                });
                return;
            }

            try
            {
                var extractor = MetadataExtractorFactory.GetExtractor(platformId);
                var metadata = await extractor.Extract(romPath);

                if (metadata != null && !string.IsNullOrEmpty(metadata.Title))
                {
                    await _cache.SaveMetadata(romPath, metadata);

                    cached = await _cache.GetCachedMetadata(romPath);

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        game.Title = cached.Title;
                        game.CoverImagePath = cached.CoverImagePath;
                        game.LogoImagePath = cached.LogoImagePath;
                    });

                    MetadataExtracted?.Invoke(game, metadata);
                }
            }
            catch { }
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