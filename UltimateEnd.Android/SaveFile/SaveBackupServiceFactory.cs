using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Android.SaveFile
{
    public class SaveBackupServiceFactory(GoogleDriveService driveService) : ISaveBackupServiceFactory
    {
        private readonly GoogleDriveService _driveService = driveService;

        public ISaveBackupService? CreateService(IEmulatorCommand command)
        {
            if (command.IsRetroArch) return new RetroArchSaveBackupService(_driveService);

            var folderPicker = FolderPickerFactory.Create?.Invoke();

            var melonds = new MelonDSSaveBackupService(_driveService, command, folderPicker);
            var edenstandard = new SwitchSaveBackupService(_driveService, command, folderPicker);
            var edenlegacy = new SwitchSaveBackupService(_driveService, command, folderPicker, "dev.legacy.eden_emulator");
            var edenoptimized = new SwitchSaveBackupService(_driveService, command, folderPicker, "com.miHoYo.Yuanshen");

            return command.Id switch
            {
                "ppsspp" => new PPSSPPSaveBackupService(_driveService, command, folderPicker),
                "melonds"  => melonds,
                "melondsdual022" => melonds,
                "melondsdual041" => melonds,
                "edenstandard" => edenstandard,
                "edenlegacy" => edenlegacy,
                "edenoptimized" => edenoptimized,
                _ => null,
            };
        }

        public bool IsSupported(IEmulatorCommand command)
        {
            if (command.IsRetroArch) return true;

            return command.Id switch
            {
                "ppsspp" => true,
                "melonds" => true,
                "melondsdual022" => true,
                "melondsdual041" => true,
                "edenstandard" => true,
                "edenlegacy" => true,
                "edenoptimized" => true,
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