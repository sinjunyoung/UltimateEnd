using System;
using System.IO;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.SaveFile
{
    public class CemuSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command, string? emulatorNameOrPath = null) : UltimateEnd.SaveFile.Cemu.SaveBackupServiceBase(driveService, command)
    {
        private readonly string? _emulatorNameOrPath = emulatorNameOrPath;

        protected override string GetBasePath(IEmulatorCommand command)
        {
            if (!string.IsNullOrEmpty(_emulatorNameOrPath))
            {
                var path = ResolveEmulatorPath(_emulatorNameOrPath);

                if (!string.IsNullOrEmpty(path) && IsValidPath(path)) return path;
            }

            var defaultPath = GetDefaultCemuPath();

            if (!string.IsNullOrEmpty(defaultPath) && IsValidPath(defaultPath)) return defaultPath;

            throw new InvalidOperationException("에뮬레이터 경로를 찾을 수 없습니다.");
        }

        private static string? ResolveEmulatorPath(string nameOrPath)
        {
            try
            {
                if (Path.IsPathRooted(nameOrPath)) return nameOrPath;

                var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                return Path.Combine(roaming, nameOrPath);
            }
            catch
            {
                return null;
            }
        }

        private static string? GetDefaultCemuPath()
        {
            try
            {
                var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var cemuPath = Path.Combine(roaming, "Cemu");

                if (Directory.Exists(cemuPath)) return cemuPath;

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            try
            {
                var savePath = Path.Combine(path, "mlc01", "usr", "save");

                return Directory.Exists(savePath);
            }
            catch
            {
                return false;
            }
        }
    }
}