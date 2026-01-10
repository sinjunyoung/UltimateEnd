using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public abstract class SaveBackupServiceBase : ISaveBackupService
    {
        protected readonly GoogleDriveService _driveService;
        private static readonly Dictionary<string, string> _folderCache = [];

        protected SaveBackupServiceBase(GoogleDriveService driveService)
        {
            _driveService = driveService;
        }

        #region Abstract Methods - 하위 클래스에서 구현 필요

        protected abstract string EmulatorName { get; }

        protected abstract string? GetGameIdentifier(GameMetadata game);

        protected abstract string[] FindSaveFilePaths(GameMetadata game, string gameId, SaveBackupMode mode);

        protected abstract void RestoreSaveFilesToDisk(byte[] zipData, GameMetadata game, string gameId, SaveBackupMode mode);

        #endregion

        #region Google Drive 폴더 관리 (공통)

        protected async Task<string> GetOrCreateDriveFolderAsync(params string[] subFolders)
        {
            var cacheKey = string.Join("/", new[] { EmulatorName }.Concat(subFolders));

            if (_folderCache.TryGetValue(cacheKey, out var cachedId)) return cachedId;

            var ultimateEndId = await GetOrCreateSingleFolderAsync("UltimateEnd", null);
            var emulatorId = await GetOrCreateSingleFolderAsync(EmulatorName, ultimateEndId);
            var currentParentId = emulatorId;

            foreach (var folderName in subFolders) currentParentId = await GetOrCreateSingleFolderAsync(folderName, currentParentId);

            _folderCache[cacheKey] = currentParentId;

            return currentParentId;
        }

        private async Task<string> GetOrCreateSingleFolderAsync(string folderName, string? parentId)
        {
            var folderId = await _driveService.FindFileByNameAsync(folderName, parentId);
            return folderId ?? await _driveService.CreateFolderAsync(folderName, parentId);
        }

        #endregion

        #region Zip 압축/해제 (공통)

        protected static byte[] CompressFiles(string[] filePaths, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var filePath in filePaths)
                {
                    if (!File.Exists(filePath)) continue;

                    var fileName = Path.GetFileName(filePath);
                    var entry = archive.CreateEntry(fileName, compressionLevel);

                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(filePath);
                    fileStream.CopyTo(entryStream);
                }
            }

            return memoryStream.ToArray();
        }

        protected static byte[] CompressFilesWithStructure(string[] filePaths, string baseDirectory, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
            {
                foreach (var filePath in filePaths)
                {
                    if (!File.Exists(filePath)) continue;

                    var relativePath = Path.GetRelativePath(baseDirectory, filePath);
                    var entry = archive.CreateEntry(relativePath, compressionLevel);

                    using var entryStream = entry.Open();
                    using var fileStream = File.OpenRead(filePath);
                    fileStream.CopyTo(entryStream);
                }
            }

            return memoryStream.ToArray();
        }

        protected static void BackupExistingFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            var backupPath = filePath + ".backup";

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            File.Move(filePath, backupPath);
        }

        protected static void BackupExistingDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;

            var backupPath = directoryPath + ".backup";

            if (Directory.Exists(backupPath)) Directory.Delete(backupPath, true);

            Directory.Move(directoryPath, backupPath);
        }

        #endregion

        #region ISaveBackupService 구현

        public virtual async Task<bool> BackupSaveAsync(GameMetadata game, SaveBackupMode mode = SaveBackupMode.NormalSave)
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
                        await UploadBackupAsync(gameId, saveFiles, "_SAVE", folderId);
                        hasAnyBackup = true;
                    }
                }

                if (mode == SaveBackupMode.SaveState || mode == SaveBackupMode.Both)
                {
                    var stateFiles = FindSaveFilePaths(game, gameId, SaveBackupMode.SaveState);

                    if (stateFiles?.Length > 0)
                    {
                        await UploadBackupAsync(gameId, stateFiles, "_STATE", folderId);
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

        public virtual async Task<List<SaveBackupInfo>> GetBackupListAsync(GameMetadata game, int limit = 20)
        {
            try
            {
                var gameId = GetGameIdentifier(game);

                if (string.IsNullOrEmpty(gameId)) return [];

                var folderId = await GetOrCreateDriveFolderAsync();
                var backups = await _driveService.FindFilesByPrefixAsync(gameId, folderId, limit);

                return [.. backups
                    .Select(b => new SaveBackupInfo
                    {
                        FileId = b.FileId,
                        FileName = b.FileName,
                        ModifiedTime = b.ModifiedTime,
                        Mode = ParseModeFromFileName(b.FileName)
                    })
                    .OrderByDescending(b => b.ModifiedTime)];
            }
            catch
            {
                return [];
            }
        }

        public virtual async Task<bool> RestoreSaveAsync(GameMetadata game, string fileId)
        {
            try
            {
                var gameId = GetGameIdentifier(game);

                if (string.IsNullOrEmpty(gameId)) return false;

                var zipData = await _driveService.DownloadFileAsync(fileId);

                if (zipData == null) return false;

                var backups = await GetBackupListAsync(game, 100);
                var backup = backups.FirstOrDefault(b => b.FileId == fileId);
                var mode = backup?.Mode ?? SaveBackupMode.NormalSave;

                RestoreSaveFilesToDisk(zipData, game, gameId, mode);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public virtual async Task<bool> RestoreSaveAsync(GameMetadata game, SaveBackupMode? mode = null)
        {
            try
            {
                var gameId = GetGameIdentifier(game);

                if (string.IsNullOrEmpty(gameId)) return false;

                var folderId = await GetOrCreateDriveFolderAsync();
                bool success = true;
                var effectiveMode = mode ?? SaveBackupMode.Both;

                if (effectiveMode == SaveBackupMode.NormalSave || effectiveMode == SaveBackupMode.Both)
                {
                    var saves = await _driveService.FindFilesByPrefixAsync($"{gameId}_SAVE", folderId, 1);

                    if (saves.Count > 0)
                    {
                        var zipData = await _driveService.DownloadFileAsync(saves[0].FileId);

                        if (zipData != null)
                            RestoreSaveFilesToDisk(zipData, game, gameId, SaveBackupMode.NormalSave);
                        else
                            success = false;
                    }
                }

                if (effectiveMode == SaveBackupMode.SaveState || effectiveMode == SaveBackupMode.Both)
                {
                    var states = await _driveService.FindFilesByPrefixAsync($"{gameId}_STATE", folderId, 1);

                    if (states.Count > 0)
                    {
                        var zipData = await _driveService.DownloadFileAsync(states[0].FileId);

                        if (zipData != null)
                            RestoreSaveFilesToDisk(zipData, game, gameId, SaveBackupMode.SaveState);
                        else
                            success = false;
                    }
                }

                return success;
            }
            catch
            {
                return false;
            }
        }

        public virtual async Task<bool> HasBackupAsync(GameMetadata game, SaveBackupMode? mode = null)
        {
            try
            {
                var gameId = GetGameIdentifier(game);

                if (string.IsNullOrEmpty(gameId)) return false;

                var folderId = await GetOrCreateDriveFolderAsync();

                if (mode == SaveBackupMode.NormalSave)
                {
                    var saves = await _driveService.FindFilesByPrefixAsync($"{gameId}_SAVE", folderId, 1);
                    return saves.Count > 0;
                }
                else if (mode == SaveBackupMode.SaveState)
                {
                    var states = await _driveService.FindFilesByPrefixAsync($"{gameId}_STATE", folderId, 1);
                    return states.Count > 0;
                }
                else
                {
                    var saves = await _driveService.FindFilesByPrefixAsync($"{gameId}_SAVE", folderId, 1);
                    var states = await _driveService.FindFilesByPrefixAsync($"{gameId}_STATE", folderId, 1);
                    return saves.Count > 0 || states.Count > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 헬퍼 메서드

        private async Task UploadBackupAsync(string gameId, string[] files, string suffix, string folderId)
        {
            var zipData = CompressFiles(files);
            var fileName = $"{gameId}{suffix}.zip";

            var existingBackups = await _driveService.FindFilesByPrefixAsync($"{gameId}{suffix}", folderId, 100);

            foreach (var backup in existingBackups) await _driveService.DeleteFileAsync(backup.FileId);

            await _driveService.UploadFileAsync(fileName, zipData, folderId);
        }

        protected static SaveBackupMode ParseModeFromFileName(string fileName)
        {
            if (fileName.Contains("_STATE")) return SaveBackupMode.SaveState;
            if (fileName.Contains("_SAVE")) return SaveBackupMode.NormalSave;
            if (fileName.Contains("_BOTH")) return SaveBackupMode.Both;

            return SaveBackupMode.NormalSave;
        }

        protected static string GetModeText(SaveBackupMode mode)
        {
            return mode switch
            {
                SaveBackupMode.NormalSave => "일반 세이브",
                SaveBackupMode.SaveState => "스테이트 세이브",
                SaveBackupMode.Both => "세이브",
                _ => "세이브"
            };
        }

        #endregion
    }
}