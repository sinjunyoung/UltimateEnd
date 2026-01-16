using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class EmulatorUrlProvider
    {
        private static EmulatorUrlProvider? _instance;

        public static EmulatorUrlProvider Instance => _instance ??= new EmulatorUrlProvider();

        private Dictionary<string, EmulatorUrlInfo>? _emulatorUrls;
        private Dictionary<string, string>? _coreUrls;
        private DateTime _lastFetchTime = DateTime.MinValue;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);

        private EmulatorUrlProvider() { }

        public string? EmulatorDocumentId { get; set; } = "1CD15-wIIDNMcWyCibvc2FzzedQVVlMStwKqODcEgfhM";

        public string? CoreDocumentId { get; set; }

        public async Task<string?> GetEmulatorDownloadUrlAsync(string emulatorId)
        {
            await EnsureUrlsLoadedAsync();

            if (_emulatorUrls == null || !_emulatorUrls.TryGetValue(emulatorId, out var info)) return null;

            return OperatingSystem.IsAndroid() ? info.AndroidUrl : info.DesktopUrl;
        }

        public async Task<string?> GetCoreDownloadUrlAsync(string coreName)
        {
            await EnsureUrlsLoadedAsync();

            if (_coreUrls == null || !_coreUrls.TryGetValue(coreName, out var url)) return null;

            return url;
        }

        public async Task RefreshAsync()
        {
            _emulatorUrls = null;
            _coreUrls = null;
            _lastFetchTime = DateTime.MinValue;

            await EnsureUrlsLoadedAsync();
        }

        private async Task EnsureUrlsLoadedAsync()
        {
            if (_emulatorUrls != null && DateTime.Now - _lastFetchTime < _cacheExpiration) return;

            try
            {
                if (!string.IsNullOrEmpty(EmulatorDocumentId))
                    _emulatorUrls = await EmulatorUrlDownloader.DownloadEmulatorUrlsAsync(EmulatorDocumentId);

                if (!string.IsNullOrEmpty(CoreDocumentId))
                    _coreUrls = await EmulatorUrlDownloader.DownloadCoreUrlsAsync(CoreDocumentId);

                _lastFetchTime = DateTime.Now;
            }
            catch (Exception)
            {
                _emulatorUrls ??= [];
                _coreUrls ??= [];
            }
        }
    }
}