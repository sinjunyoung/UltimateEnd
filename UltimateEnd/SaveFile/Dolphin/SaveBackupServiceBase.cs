using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile.Dolphin
{
    public abstract class SaveBackupServiceBase(GoogleDriveService driveService, IEmulatorCommand command) : SaveFile.SaveBackupServiceBase(driveService)
    {
        protected readonly IEmulatorCommand _command = command;

        protected override string EmulatorName => "Dolphin";

        protected abstract string GetDolphinBasePath(IEmulatorCommand command);

        protected override string? GetGameIdentifier(GameMetadata game)
        {
            string path = game.GetRomFullPath();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            var consoleType = DetectConsoleType(path);

            if (consoleType == DolphinConsoleType.GameCube)
            {
                var extractor = new GameCubeIdExtractor();
                return extractor.ExtractGameId(path);
            }
            else if (consoleType == DolphinConsoleType.Wii)
            {
                var extractor = new WiiIdExtractor();
                return extractor.ExtractGameId(path);
            }

            return null;
        }

        private static DolphinConsoleType DetectConsoleType(string romPath)
        {
            string ext = Path.GetExtension(romPath).ToLower();

            if (ext == ".wbfs" || ext == ".wad") return DolphinConsoleType.Wii;

            if (ext == ".gcz" || ext == ".rvz" || ext == ".wia" || ext == ".iso")
            {
                try
                {
                    var gcExtractor = new GameCubeIdExtractor();
                    var gameId = gcExtractor.ExtractGameId(romPath);

                    if (!string.IsNullOrEmpty(gameId) && gameId.Length >= 1)
                    {
                        char firstChar = gameId[0];

                        if (firstChar == 'G' || firstChar == 'D' || firstChar == 'P')
                            return DolphinConsoleType.GameCube;
                        else if (firstChar == 'R' || firstChar == 'S')
                            return DolphinConsoleType.Wii;
                    }
                }
                catch { }
            }

            return DolphinConsoleType.GameCube;
        }

        protected override string[] FindSaveFilePaths(GameMetadata game, string gameId, SaveBackupMode mode)
        {
            var basePath = GetDolphinBasePath(_command);

            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath)) return [];

            if (mode == SaveBackupMode.SaveState) return FindSaveStateFiles(basePath, gameId);

            var consoleType = DetectConsoleType(game.GetRomFullPath());

            if (consoleType == DolphinConsoleType.GameCube)
                return FindGameCubeSaveFiles(basePath, gameId);
            else if (consoleType == DolphinConsoleType.Wii)
                return FindWiiSaveFiles(basePath, gameId);

            return [];
        }

        private static string[] FindGameCubeSaveFiles(string basePath, string gameId)
        {
            var files = GameCubeIdExtractor.FindGciFiles(basePath, gameId);

            return files;
        }

        private static string[] FindWiiSaveFiles(string basePath, string titleId)
        {
            var wiiPath = Path.Combine(basePath, "Wii", "title");

            if (!Directory.Exists(wiiPath)) return [];

            string hexTitleId = ConvertTitleIdToHex(titleId);
            var savePath = Path.Combine(wiiPath, "00010000", hexTitleId, "data");

            if (!Directory.Exists(savePath)) return [];

            var files = Directory.GetFiles(savePath, "*", SearchOption.AllDirectories);
            var filtered = files.Where(f => !f.EndsWith(".backup")).ToArray();

            return filtered;
        }

        private static string ConvertTitleIdToHex(string titleId)
        {
            if (titleId.Length < 4) return titleId;

            string gameCode = titleId[..4];
            string hex = string.Empty;

            foreach (char c in gameCode) hex += ((int)c).ToString("X2");

            return hex.ToUpper();
        }

        private static string[] FindSaveStateFiles(string basePath, string gameId)
        {
            var statePath = Path.Combine(basePath, "StateSaves");

            if (!Directory.Exists(statePath)) return [];

            string searchPattern = gameId;
            var allFiles = Directory.GetFiles(statePath, $"{searchPattern}.*", SearchOption.TopDirectoryOnly);
            var stateFiles = allFiles.Where(f =>
            {
                string ext = Path.GetExtension(f);
                return ext.Length >= 3 && ext[0] == '.' && ext[1] == 's' && char.IsDigit(ext[2]);
            }).ToArray();

            return stateFiles;
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
                        var zipData = CompressFiles(saveFiles);
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
                    throw new FileNotFoundException($"{modeText} 파일을 찾을 수 없습니다. (Game ID: {gameId})");
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
            var basePath = GetDolphinBasePath(_command);

            if (string.IsNullOrEmpty(basePath)) throw new InvalidOperationException("Dolphin 경로를 찾을 수 없습니다.");

            if (mode == SaveBackupMode.SaveState)
            {
                RestoreSaveStateFiles(zipData, basePath); 
                return;
            }

            var consoleType = DetectConsoleType(game.GetRomFullPath());

            if (consoleType == DolphinConsoleType.GameCube)
                RestoreGameCubeSaveFiles(zipData, basePath, gameId);
            else if (consoleType == DolphinConsoleType.Wii)
                RestoreWiiSaveFiles(zipData, basePath, gameId);
        }

        private static void RestoreGameCubeSaveFiles(byte[] zipData, string basePath, string gameId)
        {
            var gcPath = Path.Combine(basePath, "GC");
            Directory.CreateDirectory(gcPath);

            string regionFolder = GameCubeIdExtractor.GetRegion(gameId);
            var targetPath = Path.Combine(gcPath, regionFolder, "Card A");
            Directory.CreateDirectory(targetPath);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var destinationPath = Path.Combine(targetPath, entry.Name);

                if (File.Exists(destinationPath)) BackupExistingFile(destinationPath);

                entry.ExtractToFile(destinationPath, true);
            }
        }

        private static void RestoreWiiSaveFiles(byte[] zipData, string basePath, string titleId)
        {
            string hexTitleId = ConvertTitleIdToHex(titleId);
            var targetPath = Path.Combine(basePath, "Wii", "title", "00010000", hexTitleId, "data");

            Directory.CreateDirectory(targetPath);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var destinationPath = Path.Combine(targetPath, entry.FullName);
                var directory = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                if (File.Exists(destinationPath)) BackupExistingFile(destinationPath);

                using var entryStream = entry.Open();
                using var fileStream = File.Create(destinationPath);
                entryStream.CopyTo(fileStream);
            }
        }

        private static void RestoreSaveStateFiles(byte[] zipData, string basePath)
        {
            var statePath = Path.Combine(basePath, "StateSaves");
            Directory.CreateDirectory(statePath);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var destinationPath = Path.Combine(statePath, entry.Name);

                if (File.Exists(destinationPath)) BackupExistingFile(destinationPath);

                entry.ExtractToFile(destinationPath, true);
            }
        }
    }
}