using UltimateEnd.Models;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Android.SaveFile
{
    public class RetroArchSaveBackupService(GoogleDriveService driveService) : RetroArchSaveBackupServiceBase(driveService)
    {
        private const string AndroidRetroArchPath = "/storage/emulated/0/RetroArch";

        protected override (string retroArchDir, IEmulatorCommand command)? GetRetroArchInfo(GameMetadata game)
        {
            try
            {
                var mappedPlatformId = GetMappedPlatformId(game.PlatformId!);
                var command = GetEmulatorCommand(mappedPlatformId, game.EmulatorId);

                if (!command.IsRetroArch) return null;

                return (AndroidRetroArchPath, command);
            }
            catch { return null; }
        }
    }
}