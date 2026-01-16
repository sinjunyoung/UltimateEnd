using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public static class EmulatorUrlDownloader
    {
        private const int TIMEOUT_SECONDS = 10;
        private static readonly HttpClient _httpClient = new();

        public static async Task<Dictionary<string, EmulatorUrlInfo>> DownloadEmulatorUrlsAsync(string documentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("문서 ID가 비어있습니다.", nameof(documentId));

            try
            {
                var csvUrl = $"https://docs.google.com/spreadsheets/d/{documentId}/export?format=csv";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

                using var response = await _httpClient.GetAsync(csvUrl, cts.Token);
                response.EnsureSuccessStatusCode();

                var csvContent = await response.Content.ReadAsStringAsync(cts.Token);
                return ParseEmulatorUrlsFromCsv(csvContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"에뮬레이터 URL 다운로드 실패: {ex.Message}", ex);
            }
        }

        public static async Task<Dictionary<string, string>> DownloadCoreUrlsAsync(string documentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(documentId))
                throw new ArgumentException("문서 ID가 비어있습니다.", nameof(documentId));

            try
            {
                var csvUrl = $"https://docs.google.com/spreadsheets/d/{documentId}/export?format=csv";

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

                using var response = await _httpClient.GetAsync(csvUrl, cts.Token);
                response.EnsureSuccessStatusCode();

                var csvContent = await response.Content.ReadAsStringAsync(cts.Token);

                return ParseCoreUrlsFromCsv(csvContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"코어 URL 다운로드 실패: {ex.Message}", ex);
            }
        }

        private static Dictionary<string, EmulatorUrlInfo> ParseEmulatorUrlsFromCsv(string csvContent)
        {
            var urlDict = new Dictionary<string, EmulatorUrlInfo>(StringComparer.OrdinalIgnoreCase);
            var lines = csvContent.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 3)
                    continue;

                var os = parts[0].Trim().Trim('"');
                var name = parts[1].Trim().Trim('"');
                var url = parts[2].Trim().Trim('"');

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                    continue;

                var emulatorId = name.ToLowerInvariant();

                if (!urlDict.TryGetValue(emulatorId, out var info))
                {
                    info = new EmulatorUrlInfo();
                    urlDict[emulatorId] = info;
                }

                if (os.Equals("Desktop", StringComparison.OrdinalIgnoreCase))
                    info.DesktopUrl = url;
                else if (os.Equals("Android", StringComparison.OrdinalIgnoreCase))
                    info.AndroidUrl = url;
            }

            return urlDict;
        }

        private static Dictionary<string, string> ParseCoreUrlsFromCsv(string csvContent)
        {
            var coreDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = csvContent.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length < 2)
                    continue;

                var coreName = parts[0].Trim().Trim('"');
                var downloadUrl = parts[1].Trim().Trim('"');

                if (!string.IsNullOrEmpty(coreName) && !string.IsNullOrEmpty(downloadUrl))
                    coreDict[coreName] = downloadUrl;
            }

            return coreDict;
        }
    }
}