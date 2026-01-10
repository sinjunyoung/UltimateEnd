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
            
            return command.Id switch
            {
                "ppsspp" => new PPSSPPSaveBackupService(_driveService, command, folderPicker),
                "melonds"  => melonds,
                // "melonds.nightly" => melonds,
                "melondsdual022" => melonds,
                "melondsdual041" => melonds,
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
                // "melonds.nightly" => true,
                "melondsdual022" => true,
                "melondsdual041" => true,
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