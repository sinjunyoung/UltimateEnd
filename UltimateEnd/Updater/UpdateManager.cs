using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using UltimateEnd.Services;

namespace UltimateEnd.Updater
{
    public class UpdateManager
    {
        private readonly string _githubRepo;
        private readonly HttpClient _httpClient;
        private readonly IUpdater _platformUpdater;

        public UpdateManager(IUpdater platformUpdater, string githubRepo = "sinjunyoung/UltimateEnd")
        {
            _platformUpdater = platformUpdater;
            _githubRepo = githubRepo;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("UltimateEnd/1.0");
        }

        public async Task<bool> CheckAndUpdateAsync(IProgress<UpdateProgress> progress = null)
        {
            try
            {
                var release = await GetLatestReleaseAsync();
                await _platformUpdater.PerformUpdateAsync(release, progress);
                await DialogService.Instance.HideLoading();

                return true;
            }
            catch (Exception ex)
            {
                progress?.Report(new UpdateProgress { Status = $"오류: {ex.Message}", Progress = 0.0 });

                return false;
            }
        }

        public async Task<string> GetLatestVersionAsync()
        {
            var release = await GetLatestReleaseAsync();

            return release?.TagName;
        }

        private async Task<GitHubRelease> GetLatestReleaseAsync()
        {
            var url = $"https://api.github.com/repos/{_githubRepo}/releases/latest";
            var json = await _httpClient.GetStringAsync(url);

            return JsonSerializer.Deserialize<GitHubRelease>(json);
        }
    }
}