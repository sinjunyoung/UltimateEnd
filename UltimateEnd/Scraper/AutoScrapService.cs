using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Models;
using UltimateEnd.Scraper.Helpers;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Scraper
{
    public class AutoScrapService : IDisposable
    {
        #region Singleton

        private static AutoScrapService? _instance;
        private static readonly object _lock = new();
        private readonly SemaphoreSlim _startSemaphore = new(1, 1);

        public static AutoScrapService Instance
        {
            get
            {
                if (_instance == null)
                    lock (_lock) _instance ??= new AutoScrapService();

                return _instance;
            }
        }

        #endregion

        #region Events

        public event EventHandler<AutoScrapProgressEventArgs>? ProgressChanged;
        public event EventHandler<AutoScrapCompletedEventArgs>? ScrapCompleted;

        #endregion

        #region Fields

        private bool _isRunning;        
        private bool _disposed;
        private bool _isPaused;

        private readonly object _runningLock = new();

        private CancellationTokenSource? _cts;
        private Task? _currentTask;
        private List<GameMetadata>? _pausedGames;

        #endregion

        #region Properties

        public bool IsRunning
        {
            get
            {
                lock (_runningLock) return _isRunning;
            }
            private set
            {
                lock (_runningLock) _isRunning = value;
            }
        }

        #endregion

        private AutoScrapService() { }

        #region Public Methods

        public async void Start(IEnumerable<GameMetadata>? games)
        {
            if (!await _startSemaphore.WaitAsync(0)) return;

            try
            {
                Stop();

                if (games == null || !ScreenScraperConfig.Instance.EnableAutoScrap) return;

                var gameList = games.ToList();

                if (gameList.Count == 0) return;

                if (!await NetworkHelper.IsInternetAvailableAsync())
                {
                    ReportProgress(0, 0, "인터넷 연결을 확인할 수 없습니다.", null);
                    return;
                }

                lock (_runningLock)
                {
                    if (_isRunning || (_currentTask != null && !_currentTask.IsCompleted))
                        _cts?.Cancel();

                    _isRunning = true;
                    _isPaused = false;
                }

                _pausedGames = await GetGamesNeedingMediaAsync(gameList);

                if (_pausedGames.Count == 0)
                {
                    IsRunning = false;
                    return;
                }

                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                _currentTask = Task.Run(() => ProcessGameListAsync(_pausedGames, _cts.Token), _cts.Token);
            }
            finally
            {
                _startSemaphore.Release();
            }
        }

        public void Stop()
        {
            lock (_runningLock)
            {
                if (!_isRunning) return;

                _isRunning = false;
                _isPaused = false;
                _pausedGames = null;
            }

            _cts?.Cancel();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            Stop();
            _cts?.Dispose();
            _startSemaphore?.Dispose();
        }

        #endregion

        #region Private Methods

        private async Task ProcessGameListAsync(List<GameMetadata> gamesToScrap, CancellationToken ct)
        {
            try
            {
                var filteredGames = gamesToScrap;

                if (filteredGames.Count == 0)
                {
                    IsRunning = false;
                    return;
                }

                ReportProgress(0, filteredGames.Count, "자동 스크래핑 시작...", null);

                using var service = new ScreenScraperService();

                int successCount = 0;
                int failedCount = 0;

                for (int i = 0; i < filteredGames.Count; i++)
                {
                    if (ct.IsCancellationRequested)
                    {
                        lock (_runningLock)
                            if (_isPaused) _pausedGames = [.. filteredGames.Skip(i)];

                        break;
                    }

                    var game = filteredGames[i];

                    try
                    {
                        var screenScraperSystemId = PlatformInfoService.GetScreenScraperSystemId(game.PlatformId);

                        if (screenScraperSystemId == ScreenScraperSystemId.NotSupported)
                        {
                            failedCount++;
                            ReportProgress(i + 1, filteredGames.Count, $"건너뜀: {game.DisplayTitle}", game);

                            continue;
                        }

                        ReportProgress(i + 1, filteredGames.Count, $"자동 스크래핑 중: {game.DisplayTitle}", game);

                        var result = await service.ScrapGameAsync(game, screenScraperSystemId, null, ct);

                        if (result.IsSuccess)
                        {
                            successCount++;
                            AllGamesManager.Instance.UpdateGame(game);
                            AllGamesManager.Instance.SavePlatformGames(game.PlatformId);

                            ScrapCompleted?.Invoke(this, new AutoScrapCompletedEventArgs
                            {
                                Game = game,
                                Success = true,
                                Message = result.ResultType == ScrapResultType.Cached ? "캐시" : "완료"
                            });
                        }
                        else
                        {
                            failedCount++;

                            ScrapCompleted?.Invoke(this, new AutoScrapCompletedEventArgs
                            {
                                Game = game,
                                Success = false,
                                Message = result.Message
                            });

                            if (result.ResultType == ScrapResultType.ApiLimitExceeded) break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        lock (_runningLock)
                            if (_isPaused) _pausedGames = [.. filteredGames.Skip(i)];

                        throw;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        ScrapCompleted?.Invoke(this, new AutoScrapCompletedEventArgs
                        {
                            Game = game,
                            Success = false,
                            Message = $"오류: {ex.Message}"
                        });
                    }

                    if (i < filteredGames.Count - 1 && !ct.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(ScreenScraperConfig.Instance.DelayBetweenRequestsMs, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            lock (_runningLock)
                                if (_isPaused) _pausedGames = [.. filteredGames.Skip(i + 1)];

                            break;
                        }
                    }
                }

                ReportProgress(filteredGames.Count, filteredGames.Count,
                    $"완료 (성공: {successCount}, 실패: {failedCount})", null);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoScrap] 예외: {ex.Message}");
            }
            finally
            {
                IsRunning = false;
            }
        }

        private static async Task<List<GameMetadata>> GetGamesNeedingMediaAsync(List<GameMetadata> games)
        {
            var condition = ScreenScraperConfig.Instance.ScrapConditionType;

            var filteredGames = new List<GameMetadata>();

            foreach (var game in games)
            {
                if (game.Ignore) continue;

                var romPath = game.GetRomFullPath();

                var screenScraperSystemId = PlatformInfoService.GetScreenScraperSystemId(game.PlatformId);

                if (screenScraperSystemId != ScreenScraperSystemId.NotSupported)
                {
                    var isArcade = ScreenScraperSystemClassifier.IsArcadeSystem(screenScraperSystemId);
                    var cacheKey = CacheKeyBuilder.Build(screenScraperSystemId, romPath, isArcade, null);

                    if (await ScreenScraperCache.IsFailedResultAsync(cacheKey)) continue;
                }

                if (condition == ScrapCondition.None)
                {
                    filteredGames.Add(game);
                    continue;
                }

                if (condition == ScrapCondition.AllMediaMissing)
                {
                    if (!game.HasLogoImage && !game.HasCoverImage && !game.HasVideo) filteredGames.Add(game);

                    continue;
                }

                bool needsScrap = false;

                if ((condition & ScrapCondition.LogoMissing) != 0 && !game.HasLogoImage) needsScrap = true;
                if ((condition & ScrapCondition.CoverMissing) != 0 && !game.HasCoverImage) needsScrap = true;
                if ((condition & ScrapCondition.VideoMissing) != 0 && !game.HasVideo) needsScrap = true;

                if (needsScrap) filteredGames.Add(game);
            }

            return filteredGames;
        }

        private void ReportProgress(int current, int total, string message, GameMetadata? game)
        {
            ProgressChanged?.Invoke(this, new AutoScrapProgressEventArgs
            {
                CurrentCount = current,
                TotalCount = total,
                Message = message,
                CurrentGame = game
            });
        }

        #endregion
    }

    #region Event Args

    public class AutoScrapProgressEventArgs : EventArgs
    {
        public int CurrentCount { get; set; }

        public int TotalCount { get; set; }

        public string Message { get; set; } = string.Empty;

        public GameMetadata? CurrentGame { get; set; }
    }

    public class AutoScrapCompletedEventArgs : EventArgs
    {
        public GameMetadata Game { get; set; } = null!;

        public bool Success { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    #endregion
}