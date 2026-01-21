using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Scraper.Helpers;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Scraper
{
    public class ScreenScraperService
    {
        #region Constants
        const string PreviouslyFailedSearchMessage = "이전에 검색 실패한 항목입니다";
        #endregion

        private readonly ScreenScraperHttpClient _httpClient = ScreenScraperHttpClient.Instance;
        private readonly MediaDownloader _mediaDownloader;
        private readonly GameDataFetcher _gameDataFetcher;
        private readonly string _timeoutMessage;

        public ScreenScraperService()
        {
            _timeoutMessage = $"요청 시간이 초과되었습니다.\n스크랩 설정-계정 및 고급 설정-타임아웃\n(현재: {ScreenScraperConfig.Instance.HttpTimeoutSeconds}초)";
            _mediaDownloader = new MediaDownloader(_httpClient);
            _gameDataFetcher = new GameDataFetcher(_httpClient, _timeoutMessage);
        }

        #region Public Methods
        public async Task<ScrapResult> ScrapGameAsync(GameMetadata game, ScreenScraperSystemId systemId, Action<string> progressCallback = null, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var result = new ScrapResult();

            try
            {
                var romPath = game.GetRomFullPath();

                if (!File.Exists(romPath))
                {
                    result.ResultType = ScrapResultType.InvalidFile;
                    result.Message = "ROM 파일을 찾을 수 없습니다";
                    return result;
                }

                var isArcade = ScreenScraperSystemClassifier.IsArcadeSystem(systemId);

                string crc = null;
                long fileSize = 0;

                if (ScreenScraperConfig.Instance.PreferredSearchMethod == SearchMethod.ByCrc)
                {
                    var fileInfo = new FileInfo(romPath);
                    long maxBytes = (long)ScreenScraperConfig.CrcCalculationMaxSizeMB * 1024 * 1024;

                    if (fileInfo.Length <= maxBytes)
                    {
                        progressCallback?.Invoke("CRC 계산 중... 0%");
                        var zipInfo = await ZipFileHelper.GetZipFileInfoAsync(romPath, isArcade, ct, percentage => progressCallback?.Invoke($"CRC 계산 중... {percentage}%"));
                        crc = zipInfo.Crc;
                        fileSize = zipInfo.FileSize;
                        progressCallback?.Invoke("스크린 스크래퍼 검색 중...");
                    }
                }

                var cacheKey = CacheKeyBuilder.Build(systemId, romPath, isArcade, crc);

                if (await ScreenScraperCache.IsFailedResultAsync(cacheKey))
                {
                    result.ResultType = ScrapResultType.NotFound;
                    result.Message = PreviouslyFailedSearchMessage;
                    return result;
                }

                var cachedGame = await ScreenScraperCache.GetCachedResultAsync(cacheKey);
                GameResult scrapedGame;

                if (cachedGame != null)
                {
                    result.ResultType = ScrapResultType.Cached;
                    scrapedGame = cachedGame;
                }
                else
                {
                    progressCallback?.Invoke("스크린 스크래퍼 검색 중...");

                    var fetchResult = await _gameDataFetcher.FetchGameDataAsync(romPath, systemId, cacheKey, crc, fileSize, isArcade, ct);

                    if (fetchResult.ResultType != ScrapResultType.Success)
                    {
                        result.ResultType = fetchResult.ResultType;
                        result.Message = fetchResult.Message;
                        return result;
                    }

                    scrapedGame = fetchResult.Game;

                    if (scrapedGame?.Media == null || scrapedGame.Media.Count == 0)
                    {
                        await ScreenScraperCache.SaveFailedResultAsync(cacheKey);
                        result.ResultType = ScrapResultType.NotFound;
                        result.Message = "다운로드할 미디어가 없습니다";
                        return result;
                    }

                    result.ResultType = ScrapResultType.Success;
                }

                result.MetadataUpdated = MetadataApplier.ApplyScrapedMetadata(game, scrapedGame, isArcade, romPath);

                progressCallback?.Invoke("미디어 다운로드 중...");

                var downloadResult = await _mediaDownloader.DownloadMediaAsync(scrapedGame, romPath, ct);

                result.MediaDownloaded = downloadResult.Success.Count;

                if (downloadResult.Errors.Count > 0) result.Warnings.AddRange(downloadResult.Errors);

                var romDir = Path.GetDirectoryName(romPath);
                var romFileName = Path.GetFileNameWithoutExtension(romPath);
                var scrapDir = Path.Combine(romDir, "scrap", romFileName);

                if (downloadResult.Success.Contains(MediaDownloader.MediaKeyBoxFront))
                {
                    var coverFile = Directory.GetFiles(scrapDir, "boxFront.*").FirstOrDefault();
                    game.CoverImagePath = coverFile;
                }

                if (downloadResult.Success.Contains(MediaDownloader.MediaKeyLogo))
                {
                    var logoFile = Directory.GetFiles(scrapDir, "logo.*").FirstOrDefault();
                    game.LogoImagePath = logoFile;
                }

                if (downloadResult.Success.Contains(MediaDownloader.MediaKeyVideo))
                {
                    var videoFile = Directory.GetFiles(scrapDir, "video.*").FirstOrDefault();
                    game.VideoPath = videoFile;
                }

                game.RefreshMediaCache();

                result.Message = result.ResultType == ScrapResultType.Cached ? "캐시에서 불러옴" : "스크래핑 완료";

                return result;
            }
            catch (Exception ex)
            {
                HandleScrapException(ex, result, ct);
                return result;
            }
            finally
            {
                result.Elapsed = sw.Elapsed;
            }
        }

        public async Task<BatchScrapResult> BatchScrapGamesAsync(List<GameMetadata> games, IProgress<BatchProgress> progress = null, CancellationToken ct = default)
        {
            if (!await NetworkHelper.IsInternetAvailableAsync(ct))
            {
                var result = new BatchScrapResult
                {
                    TotalCount = games.Count,
                    FailedCount = games.Count
                };

                foreach (var game in games) result.Failures.Add((game.GetRomFullPath(), "인터넷 연결을 확인할 수 없습니다."));

                progress?.Report(new BatchProgress(0, games.Count, 0, games.Count, 0, 0, "인터넷 연결을 확인할 수 없습니다.", null));

                return result;
            }

            var sw = Stopwatch.StartNew();
            var batchResult = new BatchScrapResult { TotalCount = games.Count };
            var condition = ScreenScraperConfig.Instance.ScrapConditionType;

            var systemIdCache = BuildSystemIdCache(games);

            int processedCount = 0;
            int successCount = 0;
            int failedCount = 0;
            int cachedCount = 0;
            int skippedCount = 0;
            bool apiLimitReached = false;

            void ReportProgress(string status, GameMetadata game = null)
            {
                progress?.Report(new BatchProgress(processedCount, games.Count, successCount, failedCount, cachedCount, skippedCount, status, game));
            }

            foreach (var game in games)
            {
                if (ct.IsCancellationRequested)
                {
                    batchResult.Failures.Add((game.GetRomFullPath(), "취소됨"));
                    break;
                }

                if (GameScrapValidator.ShouldSkipGame(game, condition))
                {
                    skippedCount++;
                    processedCount++;
                    ReportProgress($"건너뜀: {Path.GetFileName(game.GetRomFullPath())}");
                    continue;
                }

                ScreenScraperSystemId systemId = systemIdCache[game.PlatformId];
                var romFileName = Path.GetFileName(game.GetRomFullPath());

                ReportProgress($"처리 중: {romFileName}");

                try
                {
                    var scrapResult = await ScrapGameAsync(game, systemId, null, ct);

                    if (scrapResult.IsSuccess)
                    {
                        successCount++;

                        if (scrapResult.ResultType == ScrapResultType.Cached) cachedCount++;

                        if (scrapResult.Warnings.Count > 0)
                        {
                            var warning = $"[경고] {string.Join(", ", scrapResult.Warnings)}";
                            batchResult.Failures.Add((game.GetRomFullPath(), warning));
                        }

                        processedCount++;
                        ReportProgress($"완료: {romFileName}", game);
                    }
                    else
                    {
                        failedCount++;
                        batchResult.Failures.Add((game.GetRomFullPath(), scrapResult.Message));

                        if (scrapResult.ResultType == ScrapResultType.ApiLimitExceeded)
                        {
                            apiLimitReached = true;
                            MarkRemainingAsFailed(games, processedCount, batchResult, ref failedCount);
                            processedCount++;
                            break;
                        }

                        processedCount++;
                        ReportProgress($"실패: {romFileName}");
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    batchResult.Failures.Add((game.GetRomFullPath(), $"예외 발생: {ex.Message}"));
                    processedCount++;
                    ReportProgress($"오류: {romFileName}");
                }

                if (processedCount < games.Count && !ct.IsCancellationRequested && !apiLimitReached)
                {
                    try
                    {
                        await Task.Delay(ScreenScraperConfig.Instance.DelayBetweenRequestsMs, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            batchResult.SuccessCount = successCount;
            batchResult.FailedCount = failedCount;
            batchResult.CachedCount = cachedCount;
            batchResult.SkippedCount = skippedCount;
            batchResult.TotalElapsed = sw.Elapsed;

            return batchResult;
        }

        #endregion

        #region Private Helper Methods
        private static Dictionary<string, ScreenScraperSystemId> BuildSystemIdCache(List<GameMetadata> games)
        {
            var systemIdCache = new Dictionary<string, ScreenScraperSystemId>();

            foreach (var platformId in games.Select(g => g.PlatformId).Distinct())
                systemIdCache[platformId] = PlatformInfoService.Instance.GetScreenScraperSystemId(platformId);

            return systemIdCache;
        }

        private static void MarkRemainingAsFailed(List<GameMetadata> games, int processedCount, BatchScrapResult batchResult, ref int failedCount)
        {
            for (int i = processedCount + 1; i < games.Count; i++)
            {
                var remainingRomPath = games[i].GetRomFullPath();
                batchResult.Failures.Add((remainingRomPath, "API 제한으로 인해 건너뜀"));
                failedCount++;
            }
        }

        private void HandleScrapException(Exception ex, ScrapResult result, CancellationToken ct)
        {
            result.ResultType = ex switch
            {
                TaskCanceledException { InnerException: TimeoutException } => ScrapResultType.Timeout,
                TaskCanceledException when !ct.IsCancellationRequested => ScrapResultType.Timeout,
                OperationCanceledException => ScrapResultType.Cancelled,
                System.Net.Http.HttpRequestException => ScrapResultType.NetworkError,
                _ => ScrapResultType.Failed
            };

            result.Message = result.ResultType switch
            {
                ScrapResultType.Timeout => _timeoutMessage,
                ScrapResultType.Cancelled => "사용자가 취소함",
                ScrapResultType.NetworkError => $"네트워크 오류: {ex.Message}",
                _ => $"예기치 않은 오류: {ex.Message}"
            };
        }
        #endregion
    }
}