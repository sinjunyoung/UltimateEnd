using System;
using System.IO;
using UltimateEnd.Models;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.SaveFile
{
    public class DolphinSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command) : UltimateEnd.SaveFile.Dolphin.SaveBackupServiceBase(driveService, command)
    {
        protected override string GetDolphinBasePath(IEmulatorCommand command)
        {
            string? roamingPath = TryGetRoamingPath();

            if (roamingPath != null) return roamingPath;


            string? documentsPath = TryGetDocumentsPath();

            if (documentsPath != null) return documentsPath;

            throw new DirectoryNotFoundException(
                "Dolphin Emulator 데이터 폴더를 찾을 수 없습니다.\n\n" +
                "확인할 경로:\n" +
                "1. %APPDATA%\\Dolphin Emulator\n" +
                "2. %USERPROFILE%\\Documents\\Dolphin Emulator\\User");
        }

        private static string? TryGetRoamingPath()
        {
            try
            {
                string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dolphinPath = Path.Combine(appDataFolder, "Dolphin Emulator");

                if (Directory.Exists(dolphinPath))
                {
                    bool hasGC = Directory.Exists(Path.Combine(dolphinPath, "GC"));
                    bool hasWii = Directory.Exists(Path.Combine(dolphinPath, "Wii"));

                    if (hasGC || hasWii) return dolphinPath;
                }
                
                return null;
            }
            catch
            {                
                return null;
            }
        }

        private static string? TryGetDocumentsPath()
        {
            try
            {
                string documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string dolphinPath = Path.Combine(documentsFolder, "Dolphin Emulator", "User");

                if (Directory.Exists(dolphinPath))
                {
                    bool hasGC = Directory.Exists(Path.Combine(dolphinPath, "GC"));
                    bool hasWii = Directory.Exists(Path.Combine(dolphinPath, "Wii"));

                    if (hasGC || hasWii) return dolphinPath;
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