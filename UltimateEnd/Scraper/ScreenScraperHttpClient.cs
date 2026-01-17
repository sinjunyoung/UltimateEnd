using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UltimateEnd.Scraper.Models;

namespace UltimateEnd.Scraper
{
    public class ScreenScraperHttpClient
    {
        private static ScreenScraperHttpClient? _instance;
        private static readonly Lock _lock = new();

        private readonly HttpClient _http;
        private readonly SemaphoreSlim _throttler;
        private bool _disposed;

        public static ScreenScraperHttpClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ScreenScraperHttpClient();
                    }
                }
                return _instance;
            }
        }

        private ScreenScraperHttpClient()
        {
            _throttler = new SemaphoreSlim(ScreenScraperConfig.Instance.MaxConcurrentConnections, ScreenScraperConfig.Instance.MaxConcurrentConnections);

            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = ScreenScraperConfig.Instance.MaxConcurrentConnections,

                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),

                AutomaticDecompression = System.Net.DecompressionMethods.All,
                ConnectTimeout = ScreenScraperConfig.Instance.ConnectionTimeout,

                EnableMultipleHttp2Connections = false,
                ResponseDrainTimeout = TimeSpan.FromSeconds(5)
            };

            _http = new HttpClient(handler)
            {
                Timeout = ScreenScraperConfig.Instance.HttpTimeout
            };

            _http.DefaultRequestHeaders.Add("User-Agent", $"{ScreenScraperConfig.Instance.ApiSoftName}/1.0");
            _http.DefaultRequestHeaders.ConnectionClose = false;
        }

        public async Task<string> GetStringAsync(string url, CancellationToken ct = default)
        {
            await _throttler.WaitAsync(ct);

            try
            {
                return await _http.GetStringAsync(url, ct);
            }
            finally
            {
                _throttler.Release();
            }
        }

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

        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_instance != null && !_instance._disposed)
                {
                    _instance._throttler?.Dispose();
                    _instance._http?.Dispose();
                    _instance._disposed = true;
                    _instance = null;
                }
            }
        }
    }
}