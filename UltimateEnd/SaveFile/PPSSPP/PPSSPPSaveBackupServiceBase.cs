using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public abstract class PPSSPPSaveBackupServiceBase(GoogleDriveService driveService, IEmulatorCommand command) : SaveBackupServiceBase(driveService)
    {
        protected readonly IEmulatorCommand _command = command;

        protected override string EmulatorName => "PPSSPP";

        protected abstract string GetPPSSPPBasePath(IEmulatorCommand command);

        protected override string? GetGameIdentifier(GameMetadata game)
        {
            var gameId = PspSaveFolderExtractor.GetSaveFolderId(game);

            if (string.IsNullOrEmpty(gameId)) gameId = PspGameIdExtractor.GetGameId(game);

            return gameId;
        }

        protected override string[] FindSaveFilePaths(GameMetadata game, string gameId, SaveBackupMode mode)
        {
            var basePath = GetPPSSPPBasePath(_command);

            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath)) return [];

            if (mode == SaveBackupMode.NormalSave)
            {
                var folderGameId = PspSaveFolderExtractor.GetSaveFolderId(game);

                if (string.IsNullOrEmpty(folderGameId)) folderGameId = PspGameIdExtractor.GetGameId(game);

                return FindNormalSaveFiles(basePath, folderGameId);
            }
            else if (mode == SaveBackupMode.SaveState)
            {
                var fileGameId = PspGameIdExtractor.GetGameId(game);

                return FindSaveStateFiles(basePath, fileGameId);
            }

            return [];
        }

        public override async Task<bool> BackupSaveAsync(GameMetadata game, SaveBackupMode mode = SaveBackupMode.NormalSave)
        {
            try
            {
                var gameId = GetGameIdentifier(game);

                if (string.IsNullOrEmpty(gameId)) throw new Exception($"{EmulatorName} 게임 식별자를 찾을 수 없습니다.");

                var folderId = await GetOrCreateDriveFolderAsync();
                bool hasAnyBackup = false;

                if (mode == SaveBackupMode.NormalSave || mode == SaveBackupMode.Both)
                {
                    var saveFiles = FindSaveFilePaths(game, gameId, SaveBackupMode.NormalSave);

                    if (saveFiles?.Length > 0)
                    {
                        var firstFile = saveFiles[0];
                        var savedataDir = Directory.GetParent(Directory.GetParent(firstFile)!.FullName)!.FullName;
                        var zipData = CompressFilesWithStructure(saveFiles, savedataDir);
                        var fileName = $"{gameId}_SAVE.zip";
                        var existingBackups = await _driveService.FindFilesByPrefixAsync($"{gameId}_SAVE", folderId, 100);

                        foreach (var backup in existingBackups) await _driveService.DeleteFileAsync(backup.FileId);

                        await _driveService.UploadFileAsync(fileName, zipData, folderId);
                        hasAnyBackup = true;
                    }
                }

                if (mode == SaveBackupMode.SaveState || mode == SaveBackupMode.Both)
                {
                    var stateFiles = FindSaveFilePaths(game, gameId, SaveBackupMode.SaveState);

                    if (stateFiles?.Length > 0)
                    {
                        var zipData = CompressFiles(stateFiles);
                        var fileName = $"{gameId}_STATE.zip";
                        var existingBackups = await _driveService.FindFilesByPrefixAsync($"{gameId}_STATE", folderId, 100);

                        foreach (var backup in existingBackups) await _driveService.DeleteFileAsync(backup.FileId);

                        await _driveService.UploadFileAsync(fileName, zipData, folderId);
                        hasAnyBackup = true;
                    }
                }

                if (!hasAnyBackup)
                {
                    var modeText = GetModeText(mode);
                    throw new FileNotFoundException($"{modeText} 파일을 찾을 수 없습니다. ({gameId})");
                }

                return true;
            }
            catch (Exception ex) when (ex is not FileNotFoundException && ex is not NotSupportedException)
            {
                throw new IOException($"백업 중 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        private static string[] FindNormalSaveFiles(string basePath, string gameId)
        {
            var savedataPath = Path.Combine(basePath, "PSP", "SAVEDATA");
            if (!Directory.Exists(savedataPath)) return [];

            var saveFolders = Directory.GetDirectories(savedataPath, $"{gameId}*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".backup"))
                .ToArray();

            if (saveFolders.Length == 0) return [];

            return [.. saveFolders
                .SelectMany(folder => Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                .Where(f => !f.EndsWith(".backup"))];
        }

        private static string[] FindSaveStateFiles(string basePath, string gameId)
        {
            var statePath = Path.Combine(basePath, "PSP", "PPSSPP_STATE");
            if (!Directory.Exists(statePath)) return [];

            return [.. Directory.GetFiles(statePath, $"{gameId}_*.*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".backup"))
                .Where(f => f.EndsWith(".ppst", StringComparison.OrdinalIgnoreCase) ||
                           f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))];
        }

        protected override void RestoreSaveFilesToDisk(byte[] zipData, GameMetadata game, string gameId, SaveBackupMode mode)
        {
            var basePath = GetPPSSPPBasePath(_command);

            if (string.IsNullOrEmpty(basePath)) throw new InvalidOperationException("PPSSPP 경로를 찾을 수 없습니다.");

            if (mode == SaveBackupMode.NormalSave) RestoreNormalSaveFiles(zipData, basePath, gameId);
            else if (mode == SaveBackupMode.SaveState) RestoreSaveStateFiles(zipData, basePath, gameId);
        }

        private static void RestoreNormalSaveFiles(byte[] zipData, string basePath, string gameId)
        {
            var savedataPath = Path.Combine(basePath, "PSP", "SAVEDATA");
            var existingFolders = Directory.GetDirectories(savedataPath, $"{gameId}*", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith(".backup"))
                    .ToArray();
            foreach (var folder in existingFolders) BackupExistingDirectory(folder);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var normalizedPath = entry.FullName.Replace('\\', '/');
                var destinationPath = Path.Combine(savedataPath, normalizedPath);
                var directory = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                entry.ExtractToFile(destinationPath, true);

                foreach (var folder in existingFolders)
                {
                    var backupFolder = folder + ".backup";

                    if (Directory.Exists(backupFolder)) Directory.Delete(backupFolder, true);
                }
            }
        }

        private static void RestoreSaveStateFiles(byte[] zipData, string basePath, string gameId)
        {
            var statePath = Path.Combine(basePath, "PSP", "PPSSPP_STATE");
            Directory.CreateDirectory(statePath);

            var existingFiles = Directory.GetFiles(statePath, $"{gameId}_*.*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".backup"))
                .ToArray();

            foreach (var file in existingFiles)
                BackupExistingFile(file);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var destinationPath = Path.Combine(statePath, entry.FullName);
                entry.ExtractToFile(destinationPath, true);
            }
        }
    }
}