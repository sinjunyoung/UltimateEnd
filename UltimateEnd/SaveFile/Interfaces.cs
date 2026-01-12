using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public interface IFormatParser
    {
        string? ParseGameId(string filePath);

        bool CanParse(string extension);
    }

    public interface IGameIdExtractor
    {
        string? ExtractGameId(string romPath);

        bool IsValidGameId(string? gameId);
    }

    public interface ISaveBackupServiceFactory
    {
        ISaveBackupService? CreateService(IEmulatorCommand command);

        bool IsSupported(IEmulatorCommand command);

        string? GetStatusMessage(IEmulatorCommand command);
    }
}
