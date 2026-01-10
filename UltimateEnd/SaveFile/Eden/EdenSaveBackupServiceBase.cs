using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public abstract class EdenSaveBackupServiceBase(GoogleDriveService driveService, IEmulatorCommand command) : SaveBackupServiceBase(driveService)
    {
        protected readonly IEmulatorCommand _command = command;

        protected override string EmulatorName => "Switch";

        protected abstract string GetEdenBasePath(IEmulatorCommand command);

        protected abstract string? GetProKeysPath();

        protected override string? GetGameIdentifier(GameMetadata game)
        {
            var keysPath = GetProKeysPath();

            if (!string.IsNullOrEmpty(keysPath)) SwitchTitleIdExtractor.SetKeysPath(keysPath);

            return SwitchTitleIdExtractor.GetTitleId(game);
        }

        protected override string[] FindSaveFilePaths(GameMetadata game, string gameId, SaveBackupMode mode)
        {
            var basePath = GetEdenBasePath(_command);

            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                return [];

            if (mode == SaveBackupMode.NormalSave)
                return FindNormalSaveFiles(basePath, gameId);
            else if (mode == SaveBackupMode.SaveState)
                return FindSaveStateFiles(basePath, gameId);

            return [];
        }

        private static string[] FindNormalSaveFiles(string basePath, string titleId)
        {
            var userSavePath = Path.Combine(basePath, "nand", "user", "save", "0000000000000000");

            if (!Directory.Exists(userSavePath)) return [];

            foreach (var saveDataDir in Directory.GetDirectories(userSavePath))
            {
                var titleIdPath = Path.Combine(saveDataDir, titleId);

                if (Directory.Exists(titleIdPath)) return [titleIdPath];
            }

            return [];
        }

        private static string[] FindSaveStateFiles(string basePath, string titleId)
        {
            var statePath = Path.Combine(basePath, "states");

            if (!Directory.Exists(statePath)) return [];

            return [.. Directory.GetFiles(statePath, $"{titleId}*.*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".backup"))];
        }

        public override async Task<bool> BackupSaveAsync(GameMetadata game, SaveBackupMode mode = SaveBackupMode.NormalSave)
        {
            try
            {
                var gameId = GetGameIdentifier(game);

                if (string.IsNullOrEmpty(gameId)) throw new Exception($"{EmulatorName} 게임 식별자(Title ID)를 찾을 수 없습니다.");

                var folderId = await GetOrCreateDriveFolderAsync();
                bool hasAnyBackup = false;

                if (mode == SaveBackupMode.NormalSave || mode == SaveBackupMode.Both)
                {
                    var saveFiles = FindSaveFilePaths(game, gameId, SaveBackupMode.NormalSave);

                    if (saveFiles?.Length > 0)
                    {
                        var titleIdFolder = saveFiles[0];
                        var allFiles = Directory.GetFiles(titleIdFolder, "*", SearchOption.AllDirectories)
                            .Where(f => !f.EndsWith(".backup"))
                            .ToArray();

                        var zipData = CompressFilesWithStructure(allFiles, titleIdFolder);
                        var fileName = $"{gameId}_SAVE.zip";
                        var existingBackups = await _driveService.FindFilesByPrefixAsync($"{gameId}_SAVE", folderId, 100);

                        foreach (var backup in existingBackups)
                            await _driveService.DeleteFileAsync(backup.FileId);

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

                        foreach (var backup in existingBackups)
                            await _driveService.DeleteFileAsync(backup.FileId);

                        await _driveService.UploadFileAsync(fileName, zipData, folderId);
                        hasAnyBackup = true;
                    }
                }

                if (!hasAnyBackup)
                {
                    var modeText = GetModeText(mode);
                    throw new FileNotFoundException($"{modeText} 파일을 찾을 수 없습니다. (Title ID: {gameId})");
                }

                return true;
            }
            catch (Exception ex) when (ex is not FileNotFoundException && ex is not NotSupportedException)
            {
                throw new IOException($"백업 중 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        protected override void RestoreSaveFilesToDisk(byte[] zipData, GameMetadata game, string gameId, SaveBackupMode mode)
        {
            var basePath = GetEdenBasePath(_command);

            if (string.IsNullOrEmpty(basePath)) throw new InvalidOperationException("Eden 경로를 찾을 수 없습니다.");

            if (mode == SaveBackupMode.NormalSave)
                RestoreNormalSaveFiles(zipData, basePath, gameId);
            else if (mode == SaveBackupMode.SaveState)
                RestoreSaveStateFiles(zipData, basePath, gameId);
        }

        private static void RestoreNormalSaveFiles(byte[] zipData, string basePath, string titleId)
        {
            var userSavePath = Path.Combine(basePath, "nand", "user", "save", "0000000000000000");

            if (!Directory.Exists(userSavePath)) Directory.CreateDirectory(userSavePath);

            string? existingSaveDataDir = null;
            string titleIdPath;

            foreach (var saveDataDir in Directory.GetDirectories(userSavePath))
            {
                titleIdPath = Path.Combine(saveDataDir, titleId);
                if (Directory.Exists(titleIdPath))
                {
                    existingSaveDataDir = saveDataDir;
                    BackupExistingDirectory(titleIdPath);
                    break;
                }
            }

            if (existingSaveDataDir == null)
                throw new InvalidOperationException("세이브 폴더가 없습니다. 게임을 한 번 실행하여 세이브를 생성한 후 복원해주세요.");

            titleIdPath = Path.Combine(existingSaveDataDir, titleId);
            Directory.CreateDirectory(titleIdPath);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, false, System.Text.Encoding.UTF8);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var normalizedPath = entry.FullName.Replace('\\', '/');
                var destinationPath = Path.Combine(titleIdPath, normalizedPath);
                var directory = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                if (File.Exists(destinationPath)) File.Delete(destinationPath);

                using var entryStream = entry.Open();
                using var fileStream = File.Create(destinationPath);
                entryStream.CopyTo(fileStream);
                fileStream.Flush();
            }

            var backupPath = titleIdPath + ".backup";

            if (Directory.Exists(backupPath)) Directory.Delete(backupPath, true);
        }

        private static byte[] CompressFilesWithStructure(string[] filePaths, string baseDirectory)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true, System.Text.Encoding.UTF8))
            {
                foreach (var filePath in filePaths)
                {
                    if (!File.Exists(filePath)) continue;

                    var relativePath = Path.GetRelativePath(baseDirectory, filePath);
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.NoCompression);
                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(filePath);

                    fileStream.CopyTo(entryStream);
                    entryStream.Flush();
                }
            }

            return memoryStream.ToArray();
        }

        private static void RestoreSaveStateFiles(byte[] zipData, string basePath, string titleId)
        {
            var statePath = Path.Combine(basePath, "states");

            Directory.CreateDirectory(statePath);

            var existingFiles = Directory.GetFiles(statePath, $"{titleId}*.*", SearchOption.TopDirectoryOnly)
                .Where(f => !f.EndsWith(".backup"))
                .ToArray();

            foreach (var file in existingFiles) BackupExistingFile(file);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var destinationPath = Path.Combine(statePath, entry.FullName);
                entry.ExtractToFile(destinationPath, true);
            }

            foreach (var file in existingFiles)
            {
                var backupFile = file + ".backup";

                if (File.Exists(backupFile)) File.Delete(backupFile);
            }
        }
    }
}