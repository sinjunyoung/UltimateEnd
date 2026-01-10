using System;
using System.IO;
using UltimateEnd.Desktop.Models;
using UltimateEnd.Models;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.SaveFile
{
    public class RetroArchSaveBackupService(GoogleDriveService driveService) : RetroArchSaveBackupServiceBase(driveService)
    {
        protected override (string retroArchDir, IEmulatorCommand command)? GetRetroArchInfo(GameMetadata game)
        {
            try
            {
                var mappedPlatformId = GetMappedPlatformId(game.PlatformId!);
                var command = GetEmulatorCommand(mappedPlatformId, game.EmulatorId) as Command;

                if (!command.IsRetroArch) return null;

                string executable = Path.IsPathRooted(command.Executable) ? command.Executable : Path.Combine(AppContext.BaseDirectory, command.Executable);
                var retroArchDir = Path.GetDirectoryName(executable);

                if (string.IsNullOrEmpty(retroArchDir)) return null;

                return (retroArchDir, command);
            }
            catch { return null; }
        }
    }
}