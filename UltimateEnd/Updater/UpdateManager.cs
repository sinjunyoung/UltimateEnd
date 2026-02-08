using System;
using System.IO;
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
        private const string BackupFolderName = "settings_backup";

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
                progress?.Report(new UpdateProgress
                {
                    Status = "설정 파일 백업 중...",
                    Details = "기존 설정을 안전하게 보관합니다.",
                    Progress = 0.05
                });
                BackupAndDeleteConfigFiles();

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

        private static void BackupAndDeleteConfigFiles()
        {
            var factory = AppBaseFolderProviderFactory.Create();
            var settingsPath = factory.GetSettingsFolder();
            var dbPath = Path.Combine(factory.GetAssetsFolder(), "DBs");
            var backupPath = Path.Combine(Directory.GetParent(settingsPath).FullName, BackupFolderName);
            string[] settingsFiles = ["commands.txt", "platform_info.json"];

            if (!Directory.Exists(settingsPath)) return;

            if (Directory.Exists(backupPath)) Directory.Delete(backupPath, true);

            Directory.CreateDirectory(backupPath);

            // settings 파일들 백업 후 삭제
            foreach (var fileName in settingsFiles)
            {
                var sourceFile = Path.Combine(settingsPath, fileName);
            
                if (File.Exists(sourceFile))
                {
                    var destFile = Path.Combine(backupPath, fileName);
                    File.Copy(sourceFile, destFile, true);
                    File.Delete(sourceFile);
                }
            }

            // games.db는 백업 없이 바로 삭제
            string gamesdb = Path.Combine(dbPath, "games.db");

            if (File.Exists(gamesdb)) File.Delete(gamesdb);
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