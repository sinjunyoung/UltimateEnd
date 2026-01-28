using SQLite;
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
            var edenstandard = new SwitchSaveBackupService(_driveService, command);
            var edenlegacy = new SwitchSaveBackupService(_driveService, command, "dev.legacy.eden_emulator");
            var edenoptimized = new SwitchSaveBackupService(_driveService, command, "com.miHoYo.Yuanshen");
            var yuzu = new SwitchSaveBackupService(_driveService, command, "org.yuzu.yuzu_emu");
            var sumi = new SwitchSaveBackupService(_driveService, command, "com.sumi.SumiEmulator");
            var citron = new SwitchSaveBackupService(_driveService, command, "org.citron.citron_emu");
            var dolphin = new DolphinSaveBackupService(_driveService, command);
            var dolphinmmjr2 = new DolphinSaveBackupService(_driveService, command, "/storage/emulated/0/mmjr2-vbi");
            var cemu = new CemuSaveBackupService(_driveService, command);

            return command.Id switch
            {
                "ppsspp" => new PPSSPPSaveBackupService(_driveService, command, folderPicker),
                "melonds"  => melonds,
                "melondsdual022" => melonds,
                "melondsdual041" => melonds,
                "edenstandard" => edenstandard,
                "edenlegacy" => edenlegacy,
                "edenoptimized" => edenoptimized,
                "yuzu" => yuzu,
                "sumi" => sumi,
                "citron" => citron,
                "dolphin" => dolphin,
                "dolphinmmjr2" => dolphinmmjr2,
                "cemu" => cemu,
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
                "yuzu" => true,
                "sumi" => true,
                "citron" => true,
                "dolphin" => true,
                "dolphinmmjr2" => true,
                "cemu" => true,
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