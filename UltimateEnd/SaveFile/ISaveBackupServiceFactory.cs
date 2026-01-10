using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public interface ISaveBackupServiceFactory
    {
        ISaveBackupService? CreateService(IEmulatorCommand command);

        bool IsSupported(IEmulatorCommand command);

        string? GetStatusMessage(IEmulatorCommand command);
    }
}