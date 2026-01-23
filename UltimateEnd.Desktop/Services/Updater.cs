using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UltimateEnd.Updater;

namespace UltimateEnd.Desktop.Services
{
    public class Updater : IUpdater
    {
        private readonly HttpClient _httpClient = new();

        public async Task PerformUpdateAsync(GitHubRelease release, IProgress<UpdateProgress>? progress = null)
        {
            var zipAsset = release.Assets.FirstOrDefault(a => a.Name == "UltimateEnd.Desktop.zip") ?? throw new Exception("UltimateEnd.Desktop.zip 파일을 찾을 수 없습니다.");

            var tempDir = Path.Combine(Path.GetTempPath(), "UltimateEnd_Update");

            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var zipPath = Path.Combine(tempDir, "update.zip");

            try
            {
                progress?.Report(new UpdateProgress { Status = "다운로드 중", Progress = 0.2 });
                await DownloadFileAsync(zipAsset.BrowserDownloadUrl, zipPath, progress);

                progress?.Report(new UpdateProgress { Status = "압축 해제 중", Progress = 0.5 });
                var extractDir = Path.Combine(tempDir, "files");
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractDir));

                progress?.Report(new UpdateProgress { Status = "업데이트 준비 완료", Progress = 0.9 });
                CreateUpdateScript(extractDir, tempDir);

                progress?.Report(new UpdateProgress { Status = "앱 재시작 중", Progress = 1.0 });
                await Task.Delay(500);

                LaunchUpdateScriptAndExit();
            }
            catch
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch { }
                }
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
                    var p = 0.2 + (bytesRead * 0.3 / totalBytes);
                    progress?.Report(new UpdateProgress
                    {
                        Status = "다운로드 중",
                        Progress = p,
                        Details = $"{bytesRead / 1024 / 1024}MB / {totalBytes / 1024 / 1024}MB"
                    });
                }
            }
        }

        private static void CreateUpdateScript(string sourceDir, string tempDir)
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var currentExe = Environment.ProcessPath;

            var script = $@"@echo off
echo 업데이트 중...

REM 앱 종료 대기
timeout /t 2 /nobreak > nul

REM 기존 파일 백업
if not exist ""{appDir}backup"" mkdir ""{appDir}backup""
xcopy ""{appDir}*.*"" ""{appDir}backup\"" /E /Y /EXCLUDE:""{appDir}backup"" > nul

REM 새 파일 복사
xcopy ""{sourceDir}\*.*"" ""{appDir}"" /E /Y /I

REM 앱 재시작
start """" ""{currentExe}""

REM 임시 파일 정리
rd /s /q ""{tempDir}""

REM 스크립트 자체 삭제
del ""%~f0""
";
            var scriptPath = Path.Combine(tempDir, "update.bat");
            File.WriteAllText(scriptPath, script);
        }

        private static void LaunchUpdateScriptAndExit()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "UltimateEnd_Update");
            var scriptPath = Path.Combine(tempDir, "update.bat");

            Process.Start(new ProcessStartInfo
            {
                FileName = scriptPath,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
    }
}