using Android.App;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UltimateEnd.Android.Utils;
using UltimateEnd.Updater;

namespace UltimateEnd.Android.Services
{
    public class Updater(Activity activity) : IUpdater
    {
        private readonly HttpClient _httpClient = new();

        public async Task PerformUpdateAsync(GitHubRelease release, IProgress<UpdateProgress> progress = null)
        {
            var apkAsset = release.Assets.FirstOrDefault(a => a.Name == "UltimateEnd.Android.apk") ?? throw new Exception("UltimateEnd.Android.apk 파일을 찾을 수 없습니다.");
            var apkPath = Path.Combine(Application.Context.GetExternalFilesDir(null).AbsolutePath, "update.apk");

            if (File.Exists(apkPath)) File.Delete(apkPath);

            try
            {
                progress?.Report(new UpdateProgress { Status = "다운로드 중", Progress = 0.2 });
                await DownloadFileAsync(apkAsset.BrowserDownloadUrl, apkPath, progress);

                progress?.Report(new UpdateProgress { Status = "설치 준비 중", Progress = 0.95 });
                await Task.Delay(500);

                InstallApk(apkPath);

                progress?.Report(new UpdateProgress { Status = "완료", Progress = 1.0 });
            }
            catch
            {
                if (File.Exists(apkPath)) File.Delete(apkPath);

                throw;
            }
        }

        private async Task DownloadFileAsync(string url, string path, IProgress<UpdateProgress> progress)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var buffer = new byte[8192];
            long bytesRead = 0;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(path, FileMode.Create);

            int read;

            while ((read = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                bytesRead += read;

                if (totalBytes > 0)
                {
                    var p = 0.2 + (bytesRead * 0.75 / totalBytes);
                    progress?.Report(new UpdateProgress
                    {
                        Status = "다운로드 중",
                        Progress = p,
                        Details = $"{bytesRead / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB"
                    });
                }
            }
        }

        private void InstallApk(string apkPath) => ApkInstaller.Install(activity, apkPath);
    }
}