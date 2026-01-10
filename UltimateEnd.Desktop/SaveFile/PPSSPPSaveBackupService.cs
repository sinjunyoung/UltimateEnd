using System;
using System.Collections.Generic;
using System.IO;
using UltimateEnd.SaveFile;
using UltimateEnd.SaveFile.PPSSPP;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.SaveFile
{
    public class PPSSPPSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command) : UltimateEnd.SaveFile.PPSSPP.SaveBackupServiceBase(driveService, command)
    {
        protected override string GetPPSSPPBasePath(IEmulatorCommand command)
        {
            var customPath = TryReadCustomPathFromIni(command);

            if (!string.IsNullOrEmpty(customPath) && Directory.Exists(Path.Combine(customPath, "PSP", "SAVEDATA"))) return customPath;

            var candidates = new List<string>();

            if (command is Models.Command desktopCommand && !string.IsNullOrEmpty(desktopCommand.Executable))
            {
                var exePath = Path.IsPathRooted(desktopCommand.Executable) ? desktopCommand.Executable : Path.Combine(AppContext.BaseDirectory, desktopCommand.Executable);

                if (File.Exists(exePath))
                {
                    var ppssppDir = Path.GetDirectoryName(exePath);
                    candidates.Add(Path.Combine(ppssppDir!, "memstick"));
                }
            }

            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PPSSPP"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "Emulators", "PPSSPP", "memstick"));

            foreach (var path in candidates)
                if (Directory.Exists(Path.Combine(path, "PSP", "SAVEDATA"))) return path;

            return string.Empty;
        }

        private static string? TryReadCustomPathFromIni(IEmulatorCommand command)
        {
            var iniPaths = new List<string>();

            if (command is Models.Command desktopCommand && !string.IsNullOrEmpty(desktopCommand.Executable))
            {
                var exePath = Path.IsPathRooted(desktopCommand.Executable) ? desktopCommand.Executable : Path.Combine(AppContext.BaseDirectory, desktopCommand.Executable);

                if (File.Exists(exePath))
                {
                    var ppssppDir = Path.GetDirectoryName(exePath);
                    iniPaths.Add(Path.Combine(ppssppDir!, "ppsspp.ini"));
                    iniPaths.Add(Path.Combine(ppssppDir!, "memstick", "PSP", "SYSTEM", "ppsspp.ini"));
                }
            }

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            iniPaths.Add(Path.Combine(appDataPath, "PPSSPP", "ppsspp.ini"));

            foreach (var iniPath in iniPaths)
            {
                if (File.Exists(iniPath))
                {
                    var customPath = ReadCurrentDirectoryFromIni(iniPath);

                    if (!string.IsNullOrEmpty(customPath)) return customPath;
                }
            }

            return null;
        }

        private static string? ReadCurrentDirectoryFromIni(string iniPath)
        {
            try
            {
                var lines = File.ReadAllLines(iniPath);

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    if (trimmed.StartsWith("CurrentDirectory", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
                    {
                        var parts = trimmed.Split('=', 2);

                        if (parts.Length == 2) return parts[1].Trim();
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
