using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper.Helpers
{
    internal class MediaDownloader(ScreenScraperHttpClient httpClient)
    {
        public const string MediaKeyLogo = "logo";
        public const string MediaKeyBoxFront = "boxFront";
        public const string MediaKeyVideo = "video";

        private readonly ScreenScraperHttpClient _httpClient = httpClient;

        public async Task<DownloadResult> DownloadMediaAsync(GameResult game, string romPath, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            var result = new DownloadResult();

            try
            {
                if (game?.Media == null || game.Media.Count == 0)
                {
                    result.Errors.Add("다운로드할 미디어가 없습니다");
                    return result;
                }

                var saveDir = CreateMediaDirectory(romPath, result);

                if (saveDir == null) return result;

                var enabledKeys = GetEnabledMediaKeys();
                var downloadTasks = enabledKeys
                    .Where(key => game.Media.ContainsKey(key))
                    .Select(key => DownloadSingleMediaAsync(game.Media[key], key, saveDir, ct));

                var downloadResults = await Task.WhenAll(downloadTasks);

                foreach (var (key, success, error) in downloadResults)
                {
                    result.TotalCount++;

                    if (success)
                        result.Success.Add(key);
                    else if (error != null)
                        result.Errors.Add(error);
                }

                if (result.TotalCount == 0)
                    result.Errors.Add("다운로드할 미디어를 찾을 수 없습니다");

                result.ElapsedTime = sw.Elapsed;

                return result;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                    throw;

                result.Errors.Add($"다운로드 오류: {ex.Message}");

                return result;
            }
        }

        private async Task<(string key, bool success, string? error)> DownloadSingleMediaAsync(MediaInfo media, string key, string saveDir, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return (key, false, null);

            var filename = $"{key}.{media.Format}";
            var path = Path.Combine(saveDir, filename);

            try
            {
                var success = await DownloadFileAsync(media.Url, path, ct);
                return (key, success, success ? null : $"{filename} 다운로드 실패");
            }
            catch (OperationCanceledException)
            {
                return (key, false, null);
            }
            catch (Exception ex)
            {
                return (key, false, $"{filename} 다운로드 오류: {ex.Message}");
            }
        }

        private async Task<bool> DownloadFileAsync(string url, string path, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(url)) return false;

                var data = await _httpClient.GetByteArrayAsync(url, ct);

                if (data.Length == 0) return false;

                await File.WriteAllBytesAsync(path, data, ct);

                return true;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                TryDeleteFile(path);
                return false;
            }
            catch (OperationCanceledException)
            {
                TryDeleteFile(path);
                throw;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static string? CreateMediaDirectory(string romPath, DownloadResult result)
        {
            var romDir = Path.GetDirectoryName(romPath);
            var romFileName = Path.GetFileNameWithoutExtension(romPath);
            var saveDir = Path.Combine(romDir, "scrap", romFileName);

            try
            {
                Directory.CreateDirectory(saveDir);

                return saveDir;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"미디어 폴더 생성 실패: {ex.Message}");

                return null;
            }
        }

        private static string[] GetEnabledMediaKeys()
        {
            string[] allMediaKeys = [MediaKeyBoxFront, MediaKeyLogo, MediaKeyVideo];

            return [.. allMediaKeys.Where(key =>
                (key != MediaKeyLogo || ScreenScraperConfig.Instance.AllowScrapLogo) &&
                (key != MediaKeyBoxFront || ScreenScraperConfig.Instance.AllowScrapCover) &&
                (key != MediaKeyVideo || ScreenScraperConfig.Instance.AllowScrapVideo)
            )];
        }
    }
}