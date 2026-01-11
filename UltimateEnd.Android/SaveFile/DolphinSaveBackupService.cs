using System;
using System.IO;
using UltimateEnd.Models;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Android.SaveFile
{
    public class DolphinSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command, string? customBasePath = null) : UltimateEnd.SaveFile.Dolphin.SaveBackupServiceBase(driveService, command)
    {
        protected override string GetDolphinBasePath(IEmulatorCommand command)
        {
            if (customBasePath != null && !string.IsNullOrWhiteSpace(customBasePath))
                if (Directory.Exists(customBasePath) && IsValidDolphinPath(customBasePath)) return customBasePath;

            string? dolphinPath = TryGetDolphinPath();

            if (dolphinPath != null) return dolphinPath;

            throw new DirectoryNotFoundException(
                "Dolphin Emulator 데이터 폴더를 찾을 수 없습니다.\n\n" +
                "확인할 경로:\n" +
                "1. /storage/emulated/0/Android/data/org.dolphinemu.dolphinemu/files\n" +
                "2. /storage/emulated/0/mmjr2-vbi");
        }

        private static bool IsValidDolphinPath(string path)
        {
            bool hasGC = Directory.Exists(Path.Combine(path, "GC"));
            bool hasWii = Directory.Exists(Path.Combine(path, "Wii"));
            bool hasStates = Directory.Exists(Path.Combine(path, "StateSaves"));

            return hasGC || hasWii || hasStates;
        }

        private static string? TryGetDolphinPath()
        {
            try
            {
                string[] possiblePaths =
                [
                    // Official Dolphin
                    "/storage/emulated/0/Android/data/org.dolphinemu.dolphinemu/files",
                    "/sdcard/Android/data/org.dolphinemu.dolphinemu/files",
                    
                    // MMJR2-VBI
                    "/storage/emulated/0/mmjr2-vbi",
                    "/sdcard/mmjr2-vbi",
                ];

                foreach (var dolphinPath in possiblePaths)
                {
                    if (Directory.Exists(dolphinPath) && IsValidDolphinPath(dolphinPath)) return dolphinPath;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}