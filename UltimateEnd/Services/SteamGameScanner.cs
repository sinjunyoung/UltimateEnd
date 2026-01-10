using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UltimateEnd.Managers;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class SteamGameScanner
    {
        private static readonly Regex AppIdRegex = new(@"""appid""\s+""(\d+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex NameRegex = new(@"""name""\s+""([^""]+)""", RegexOptions.Compiled);

        private static readonly HashSet<string> IgnoredManifests =
        [
            "appmanifest_228980.acf",  // Steamworks Common Redistributables
            "appmanifest_996510.acf",  // Proton 3.16 Beta
            "appmanifest_961940.acf",  // Proton 3.16
            "appmanifest_930400.acf",  // Proton 3.7 Beta
            "appmanifest_858280.acf",  // Proton 3.7
            "appmanifest_1054830.acf", // Proton 4.2
            "appmanifest_1070560.acf", // Steam Linux Runtime
            "appmanifest_1113280.acf", // Proton 4.11
            "appmanifest_1245040.acf", // Proton 5.0
            "appmanifest_1391110.acf", // Steam Linux Runtime - Soldier
            "appmanifest_1628350.acf", // Steam Linux Runtime - Sniper
            "appmanifest_1420170.acf", // Proton 5.13
            "appmanifest_1580130.acf", // Proton 6.3
            "appmanifest_1887720.acf", // Proton 7.0
            "appmanifest_1493710.acf", // Proton Experimental
            "appmanifest_2180100.acf", // Proton Hotfix
            "appmanifest_2230260.acf", // Proton Next
            "appmanifest_1826330.acf", // Proton EasyAntiCheat Runtime
            "appmanifest_1161040.acf", // Proton BattlEye Runtime
        ];

        private static string? FindSteamInstallPath()
        {
            if (!OperatingSystem.IsWindows()) return null;

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");

                if (key != null)
                {
                    var path = key.GetValue("InstallPath") as string;

                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
                }
            }
            catch { }

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");

                if (key != null)
                {
                    var path = key.GetValue("InstallPath") as string;

                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
                }
            }
            catch { }

            var defaultPath = @"C:\Program Files (x86)\Steam";

            if (Directory.Exists(defaultPath)) return defaultPath;

            return null;
        }

        private static List<string> FindLibraryFolders(string steamPath)
        {
            List<string> libraryFolders = [];

            var defaultLibrary = Path.Combine(steamPath, "steamapps");

            if (Directory.Exists(defaultLibrary)) libraryFolders.Add(defaultLibrary);

            var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");

            if (!File.Exists(libraryFile)) return libraryFolders;

            try
            {
                var content = File.ReadAllText(libraryFile);
                var pathRegex = new Regex(@"""path""\s+""([^""]+)""", RegexOptions.IgnoreCase);
                var matches = pathRegex.Matches(content);

                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count > 1)
                    {
                        var path = match.Groups[1].Value.Replace(@"\\", @"\");
                        var steamappsPath = Path.Combine(path, "steamapps");

                        if (Directory.Exists(steamappsPath) && !libraryFolders.Contains(steamappsPath)) libraryFolders.Add(steamappsPath);
                    }
                }
            }
            catch { }

            return libraryFolders;
        }

        private static (string? appId, string? name) ReadManifestFile(string manifestPath)
        {
            try
            {
                var content = File.ReadAllText(manifestPath);
                var appIdMatch = AppIdRegex.Match(content);
                var nameMatch = NameRegex.Match(content);
                var appId = appIdMatch.Success ? appIdMatch.Groups[1].Value : null;
                var name = nameMatch.Success ? nameMatch.Groups[1].Value : null;

                return (appId, name);
            }
            catch
            {
                return (null, null);
            }
        }

        public static List<GameMetadata> ScanSteamGames(string systemAppsPath)
        {
            var games = new List<GameMetadata>();

            if (!OperatingSystem.IsWindows()) return games;

            var steamPath = FindSteamInstallPath();

            if (string.IsNullOrEmpty(steamPath)) return games;

            var libraryFolders = FindLibraryFolders(steamPath);
            var converter = PathConverterFactory.Create?.Invoke();
            var realSystemAppsPath = converter?.FriendlyPathToRealPath(systemAppsPath) ?? systemAppsPath;
            var steamFolder = Path.Combine(realSystemAppsPath, "steam");

            if (!Directory.Exists(steamFolder))
            {
                try
                {
                    Directory.CreateDirectory(steamFolder);
                }
                catch
                {
                    return games;
                }
            }

            foreach (var libraryFolder in libraryFolders)
            {
                try
                {
                    var manifestFiles = Directory.GetFiles(libraryFolder, "appmanifest_*.acf");

                    foreach (var manifestFile in manifestFiles)
                    {
                        var fileName = Path.GetFileName(manifestFile);

                        if (IgnoredManifests.Contains(fileName)) continue;

                        var (appId, name) = ReadManifestFile(manifestFile);

                        if (string.IsNullOrEmpty(appId)) continue;

                        if (string.IsNullOrEmpty(name))
                            name = $"App #{appId}";

                        var dummyFilePath = Path.Combine(steamFolder, appId + ".steam");

                        if (!File.Exists(dummyFilePath))
                        {
                            try
                            {
                                File.WriteAllText(dummyFilePath, appId);
                            }
                            catch { }
                        }

                        var game = new GameMetadata
                        {
                            PlatformId = GameMetadataManager.SteamKey,
                            RomFile = appId + ".steam",
                            Title = name,
                            EmulatorId = "steam"
                        };

                        game.SetBasePath(steamFolder);
                        games.Add(game);
                    }
                }
                catch { }
            }

            return games;
        }

        public static List<GameMetadata> ScanSteamGames()
        {
            var systemAppsPath = AppSettings.SystemAppsPath;

            if (string.IsNullOrEmpty(systemAppsPath)) return [];

            return ScanSteamGames(systemAppsPath);
        }
    }
}