using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile.Switch
{

    public abstract class SaveBackupServiceBase(GoogleDriveService driveService, IEmulatorCommand command) : SaveFile.SaveBackupServiceBase(driveService)
    {
        protected readonly IEmulatorCommand _command = command;

        protected override string EmulatorName => "Switch";

        protected abstract string GetBasePath(IEmulatorCommand command);

        protected abstract string? GetProKeysPath();

        protected override string? GetGameIdentifier(GameMetadata game)
        {
            var keysPath = GetProKeysPath();

            if (!string.IsNullOrEmpty(keysPath)) GameIdExtractor.SetKeysPath(keysPath);

            return GameIdExtractor.ExtractGameId(game);
        }

        protected override string[] FindSaveFilePaths(GameMetadata game, string gameId, SaveBackupMode mode)
        {
            var basePath = GetBasePath(_command);

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

                    var basePath = GetBasePath(_command);
                    if (!string.IsNullOrEmpty(basePath))
                    {
                        var profilesData = ProfileParser.BackupProfilesFile(basePath);
                        if (profilesData != null)
                        {
                            var profilesFileName = "profiles.dat";
                            var existingProfiles = await _driveService.FindFilesByPrefixAsync("profiles", folderId, 10);

                            foreach (var profile in existingProfiles)
                                await _driveService.DeleteFileAsync(profile.FileId);

                            await _driveService.UploadFileAsync(profilesFileName, profilesData, folderId);
                        }
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
            var basePath = GetBasePath(_command);

            if (string.IsNullOrEmpty(basePath)) throw new InvalidOperationException("경로를 찾을 수 없습니다.");

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
            {
                var profileUUIDs = ProfileParser.ParseProfileUUIDs(
                    ProfileParser.GetProfilesPath(basePath));

                if (profileUUIDs.Count > 0)
                {
                    existingSaveDataDir = Path.Combine(userSavePath, profileUUIDs[0]);
                    Directory.CreateDirectory(existingSaveDataDir);
                }
                else
                {
                    throw new InvalidOperationException(
                        "세이브 폴더가 없습니다.\n\n" +
                        "다음 중 하나를 수행해주세요:\n" +
                        "1. 게임을 한 번 실행하여 세이브 폴더를 생성\n" +
                        "2. 백업한 profiles.dat 파일을 먼저 복원\n\n" +
                        "profiles.dat 복원 경로:\n" +
                        ProfileParser.GetProfilesPath(basePath));
                }
            }

            titleIdPath = Path.Combine(existingSaveDataDir, titleId);
            Directory.CreateDirectory(titleIdPath);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, false, Encoding.UTF8);

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

        public async Task<bool> RestoreProfilesFileAsync()
        {
            try
            {
                var folderId = await GetOrCreateDriveFolderAsync();
                var profilesFiles = await _driveService.FindFilesByPrefixAsync("profiles", folderId, 10);

                if (profilesFiles.Count == 0)
                    return false;

                var profilesData = await _driveService.DownloadFileAsync(profilesFiles[0].FileId);
                var basePath = GetBasePath(_command);

                if (string.IsNullOrEmpty(basePath))
                    throw new InvalidOperationException("에뮬레이터 경로를 찾을 수 없습니다.");

                ProfileParser.RestoreProfilesFile(basePath, profilesData);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] CompressFilesWithStructure(string[] filePaths, string baseDirectory)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true, Encoding.UTF8))
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