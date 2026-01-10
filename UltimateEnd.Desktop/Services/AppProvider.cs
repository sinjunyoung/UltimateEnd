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

            if (string.IsNullOrEmpty(filePath))
                return [];

            var displayName = Path.GetFileNameWithoutExtension(filePath);
            var icon = _iconProvider.GetAppIcon(filePath);

            return
            [
                new() {
                    Identifier = filePath,
                    DisplayName = displayName,
                    Icon = icon,
                    ActivityName = string.Empty
                }
            ];
        }

        public void LaunchApp(GameMetadata game)
        {
            var exePath = game.GetRomFullPath();

            if (!File.Exists(exePath))
                throw new FileNotFoundException($"실행 파일을 찾을 수 없습니다: {exePath}");

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