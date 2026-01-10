using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper
{
    public class ScreenScraperHttpClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly SemaphoreSlim _throttler;

        public ScreenScraperHttpClient()
        {
            _throttler = new SemaphoreSlim(
                ScreenScraperConfig.Instance.MaxConcurrentConnections,
                ScreenScraperConfig.Instance.MaxConcurrentConnections);

            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = ScreenScraperConfig.Instance.MaxConcurrentConnections,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                ConnectTimeout = ScreenScraperConfig.Instance.ConnectionTimeout
            };

            _http = new HttpClient(handler)
            {
                Timeout = ScreenScraperConfig.Instance.HttpTimeout
            };

            _http.DefaultRequestHeaders.Add("User-Agent", $"{ScreenScraperConfig.Instance.ApiSoftName}/1.0");
            _http.DefaultRequestHeaders.ConnectionClose = false;
        }

        public async Task<string> GetStringAsync(string url, CancellationToken ct = default) => await _http.GetStringAsync(url, ct);

        public async Task<byte[]> GetByteArrayAsync(string url, CancellationToken ct = default)
        {
            await _throttler.WaitAsync(ct);

            try
            {
                return await _http.GetByteArrayAsync(url, ct);
            }
            finally
            {
                _throttler.Release();
            }
        }

        public void Dispose()
        {
            _throttler?.Dispose();
            _http?.Dispose();
        }
    }
}