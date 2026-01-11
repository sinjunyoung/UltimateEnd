using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.SaveFile
{
    public class SaveBackupServiceFactory(GoogleDriveService driveService) : ISaveBackupServiceFactory
    {
        private readonly GoogleDriveService _driveService = driveService;

        public ISaveBackupService? CreateService(IEmulatorCommand command)
        {
            if (command.IsRetroArch) return new RetroArchSaveBackupService(_driveService);

            var sudachi = new SwitchSaveBackupService(_driveService, command);
            var yuzu = new SwitchSaveBackupService(_driveService, command, "yuzu");
            var dolphin = new DolphinSaveBackupService(_driveService, command);

            return command.Id switch
            {
                "ppsspp" => new PPSSPPSaveBackupService(_driveService, command),
                "sudachi" => sudachi,
                "yuzu" => yuzu,
                "dolphin" => dolphin,
                _ => null,
            };
        }

        public bool IsSupported(IEmulatorCommand command)
        {
            if (command.IsRetroArch) return true;

            return command.Id switch
            {
                "ppsspp" => true,
                "sudachi" => true,
                "yuzu" => true,
                "dolphin" => true,
                _ => false,
            };
        }

        public string? GetStatusMessage(IEmulatorCommand command)
        {
            if (IsSupported(command)) return null;

            return "지원하지 않는 에뮬레이터입니다.";
        }
    }
}