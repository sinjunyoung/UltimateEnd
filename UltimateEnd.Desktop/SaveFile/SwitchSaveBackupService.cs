using System;
using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public class SwitchSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command, string? emulatorNameOrPath = null) : Switch.SaveBackupServiceBase(driveService, command)
    {
        private readonly string? _emulatorNameOrPath = emulatorNameOrPath;

        protected override string GetBasePath(IEmulatorCommand command)
        {
            if (!string.IsNullOrEmpty(_emulatorNameOrPath))
            {
                var path = ResolveEmulatorPath(_emulatorNameOrPath);

                if (!string.IsNullOrEmpty(path) && IsValidPath(path)) return path;
            }

            var defaultPath = GetDefaultSudachiPath();

            if (!string.IsNullOrEmpty(defaultPath) && IsValidPath(defaultPath)) return defaultPath;

            throw new InvalidOperationException("에뮬레이터 경로를 찾을 수 없습니다.");
        }

        protected override string? GetProKeysPath()
        {
            try
            {
                var basePath = GetBasePath(_command);

                return Path.Combine(basePath, "keys", "prod.keys");
            }
            catch
            {
                return null;
            }
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

        private static string? GetDefaultSudachiPath()
        {
            try
            {
                var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var sudachiPath = Path.Combine(roaming, "sudachi");

                if (Directory.Exists(sudachiPath)) return sudachiPath;

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
                var savePath = Path.Combine(path, "nand", "user", "save");

                return Directory.Exists(savePath);
            }
            catch
            {
                return false;
            }
        }
    }
}