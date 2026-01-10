using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper.Helpers
{
    internal class GameDataFetcher(ScreenScraperHttpClient httpClient, string timeoutMessage)
    {
        const string NoSearchResultsMessage = "검색 결과가 없습니다";
        const string ApiLimitExceededMessage = "API 호출 제한 초과. 잠시 후 다시 시도하세요";
        const string CancelledMessage = "사용자가 취소함";

        private readonly ScreenScraperHttpClient _httpClient = httpClient;
        private static readonly SemaphoreSlim _apiSemaphore = new(1, 1);
        private readonly string _timeoutMessage = timeoutMessage;

        public async Task<FetchResult> FetchGameDataAsync(string romPath, ScreenScraperSystemId systemId, string cacheKey, string crc, long fileSize, bool isArcade, CancellationToken ct)
        {
            var result = new FetchResult();

            try
            {
                string fileName = Path.GetFileName(romPath);

                if (string.IsNullOrEmpty(fileName))
                {
                    result.ResultType = ScrapResultType.InvalidFile;
                    result.Message = "유효한 파일명을 추출할 수 없습니다";
                    await ScreenScraperCache.SaveFailedResultAsync(cacheKey);

                    return result;
                }

                // CacheKeyBuilder.BuildSearchFileName이 이미 ZIP 내부 파일명 추출 로직을 포함
                fileName = CacheKeyBuilder.BuildSearchFileName(romPath, isArcade);

                var url = UrlBuilder.BuildSearchUrl(fileName, systemId, crc, fileSize);
                string xmlContent;

                try
                {
                    await _apiSemaphore.WaitAsync(ct);

                    try
                    {
                        xmlContent = await _httpClient.GetStringAsync(url, ct);
                    }
                    finally
                    {
                        _apiSemaphore.Release();
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        result.ResultType = ScrapResultType.NotFound;
                        result.Message = NoSearchResultsMessage;

                        await ScreenScraperCache.SaveFailedResultAsync(cacheKey);

                        return result;
                    }
                    result.ResultType = ScrapResultType.NetworkError;
                    result.Message = $"네트워크 오류: {ex.Message}";

                    return result;
                }

                var errorMessage = ApiErrorParser.Check(xmlContent);

                if (errorMessage != null)
                {
                    if (errorMessage.Contains("제한") || errorMessage.Contains("quota") || errorMessage.Contains("exceeded"))
                    {
                        result.ResultType = ScrapResultType.ApiLimitExceeded;
                        result.Message = ApiLimitExceededMessage;
                    }
                    else
                    {
                        result.ResultType = ScrapResultType.Failed;
                        result.Message = errorMessage;

                        await ScreenScraperCache.SaveFailedResultAsync(cacheKey);
                    }

                    return result;
                }

                var game = ScreenScraperXmlParser.ParseGameInfo(xmlContent);

                if (game == null)
                {
                    result.ResultType = ScrapResultType.NotFound;
                    result.Message = NoSearchResultsMessage;

                    await ScreenScraperCache.SaveFailedResultAsync(cacheKey);

                    return result;
                }

                await ScreenScraperCache.SaveCachedResultAsync(cacheKey, game);

                result.ResultType = ScrapResultType.Success;
                result.Game = game;

                return result;
            }
            catch (Exception ex)
            {
                HandleException(ex, result, _timeoutMessage, ct);

                return result;
            }
        }

        private static void HandleException(Exception ex, FetchResult result, string timeoutMessage, CancellationToken ct)
        {
            result.ResultType = ex switch
            {
                TaskCanceledException { InnerException: TimeoutException } => ScrapResultType.Timeout,
                TaskCanceledException when !ct.IsCancellationRequested => ScrapResultType.Timeout,
                OperationCanceledException => ScrapResultType.Cancelled,
                HttpRequestException => ScrapResultType.NetworkError,
                _ => ScrapResultType.Failed
            };

            result.Message = result.ResultType switch
            {
                ScrapResultType.Timeout => timeoutMessage,
                ScrapResultType.Cancelled => CancelledMessage,
                ScrapResultType.NetworkError => $"네트워크 오류: {ex.Message}",
                _ => $"예기치 않은 오류: {ex.Message}"
            };
        }
    }
}