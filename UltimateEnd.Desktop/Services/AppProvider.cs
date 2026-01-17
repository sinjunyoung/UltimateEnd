using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Desktop.Services
{
    public class AppProvider() : IAppProvider
    {
        private readonly AppIconProvider _iconProvider = new();

        public string PlatformId => "desktop";

        public string PlatformName => "Desktop";

        public async Task<List<NativeAppInfo>> BrowseAppsAsync()
        {
            var filters = new List<FilePickerFileType>
            {
                new("실행 파일") { Patterns = ["*.exe"] },
                FilePickerFileTypes.All
            };

            var filePath = await DialogHelper.OpenFileAsync(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                filters
            );

            if (string.IsNullOrEmpty(filePath)) return [];

            var displayName = Path.GetFileNameWithoutExtension(filePath);
            var icon = _iconProvider.GetAppIcon(filePath);

            var systemAppsPath = AppSettings.SystemAppsPath;
            var converter = PathConverterFactory.Create?.Invoke();
            var realSystemAppsPath = converter?.FriendlyPathToRealPath(systemAppsPath) ?? systemAppsPath;
            var platformPath = Path.Combine(realSystemAppsPath, PlatformId);

            Directory.CreateDirectory(platformPath);

            var safeFileName = string.Join("_", displayName.Split(Path.GetInvalidFileNameChars()));
            var dummyFileName = $"{safeFileName}.desktop";
            var dummyFilePath = Path.Combine(platformPath, dummyFileName);

            File.WriteAllText(dummyFilePath, filePath);

            string? savedIconPath = null;
            if (icon != null)
            {
                var mediaPath = Path.Combine(platformPath, "media", safeFileName);
                Directory.CreateDirectory(mediaPath);

                var logoPath = Path.Combine(mediaPath, "logo.png");

                await Task.Run(() => icon.Save(logoPath));

                savedIconPath = logoPath;
            }

            return
            [
                new() {
                    Identifier = dummyFileName,
                    DisplayName = displayName,
                    Icon = icon,
                    ActivityName = savedIconPath
                }
            ];
        }

        public void LaunchApp(GameMetadata game)
        {
            var dummyPath = game.GetRomFullPath();

            if (!File.Exists(dummyPath)) throw new FileNotFoundException($"앱 정보 파일을 찾을 수 없습니다: {dummyPath}");

            var exePath = File.ReadAllText(dummyPath).Trim();

            if (!File.Exists(exePath)) throw new FileNotFoundException($"실행 파일을 찾을 수 없습니다: {exePath}");

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
    }
}