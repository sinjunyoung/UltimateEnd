using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.SaveFile
{
    public abstract class RetroArchSaveBackupServiceBase(GoogleDriveService driveService) : SaveBackupServiceBase(driveService)
    {
        private static readonly Dictionary<string, string> _persistentFolderCache = [];
        private static string _ultimateEndFolderId;
        private static string _retroArchFolderId;
        private static string _savesFolderId;

        protected override string EmulatorName => "RetroArch";

        protected override string? GetGameIdentifier(GameMetadata game) => string.IsNullOrEmpty(game.RomFile) ? null : Path.GetFileNameWithoutExtension(game.RomFile);

        protected static string GetMappedPlatformId(string platformId)
        {
            var mappingConfig = PlatformMappingService.Instance.LoadMapping();

            if (mappingConfig?.FolderMappings?.TryGetValue(platformId, out var mapped) == true) return PlatformInfoService.NormalizePlatformId(mapped);

            return PlatformInfoService.NormalizePlatformId(platformId);
        }

        protected abstract (string retroArchDir, IEmulatorCommand command)? GetRetroArchInfo(GameMetadata game);

        protected override string[] FindSaveFilePaths(GameMetadata game, string gameId, SaveBackupMode mode)
        {
            var retroArchInfo = GetRetroArchInfo(game);

            if (retroArchInfo == null) return [];

            var (retroArchDir, command) = retroArchInfo.Value;
            var coreFolderName = ExtractCoreFolderName(command.Name);

            if (mode == SaveBackupMode.NormalSave)
                return FindNormalSaveFiles(retroArchDir, coreFolderName, command.CoreName, gameId);
            if (mode == SaveBackupMode.SaveState)
                return FindSaveStateFiles(retroArchDir, coreFolderName, gameId);
            if (mode == SaveBackupMode.Both)
            {
                var normalSaves = FindNormalSaveFiles(retroArchDir, coreFolderName, command.CoreName, gameId);
                var saveStates = FindSaveStateFiles(retroArchDir, coreFolderName, gameId);
                return [.. normalSaves, .. saveStates];
            }

            return [];
        }

        private static string[] FindNormalSaveFiles(string retroArchDir, string coreFolderName, string coreName, string gameId)
        {
            var saveInfo = RetroArchSaveConfig.GetSaveInfo(coreName);
            var savesFolder = Path.Combine(retroArchDir, "saves", coreFolderName);

            if (!string.IsNullOrEmpty(saveInfo.SubFolder)) savesFolder = Path.Combine(savesFolder, saveInfo.SubFolder);

            if (!Directory.Exists(savesFolder)) return [];

            var saveFiles = saveInfo.Extensions
                .SelectMany(ext => Directory.GetFiles(savesFolder, $"{gameId}{ext}"))
                .Where(f => !f.EndsWith(".backup"))
                .ToList();

            if (saveInfo.OptionalExtensions != null)
            {
                var optionalFiles = saveInfo.OptionalExtensions
                    .SelectMany(ext => Directory.GetFiles(savesFolder, $"{gameId}{ext}"))
                    .Where(f => !f.EndsWith(".backup"));

                saveFiles.AddRange(optionalFiles);
            }

            return [.. saveFiles];
        }

        private static string[] FindSaveStateFiles(string retroArchDir, string coreFolderName, string gameId)
        {
            var statesFolder = Path.Combine(retroArchDir, "states", coreFolderName);

            if (!Directory.Exists(statesFolder)) return [];

            var stateFiles = new List<string>();

            var stateFile = Path.Combine(statesFolder, $"{gameId}.state");

            if (File.Exists(stateFile)) stateFiles.Add(stateFile);

            for (int i = 0; i <= 9; i++)
            {
                var file = Path.Combine(statesFolder, $"{gameId}.state{i}");

                if (File.Exists(file)) stateFiles.Add(file);
            }

            return [.. stateFiles];
        }

        protected override void RestoreSaveFilesToDisk(byte[] zipData, GameMetadata game, string gameId, SaveBackupMode mode)
        {
            var (retroArchDir, command) = GetRetroArchInfo(game) ?? throw new InvalidOperationException("RetroArch 정보를 찾을 수 없습니다.");
            var coreFolderName = ExtractCoreFolderName(command.Name);

            ExtractSaveFilesByMode(zipData, retroArchDir, coreFolderName, command.CoreName, mode);
        }

        public override async Task<bool> BackupSaveAsync(GameMetadata game, SaveBackupMode mode = SaveBackupMode.NormalSave)
        {
            try
            {
                var gameId = GetGameIdentifier(game);

                if (string.IsNullOrEmpty(gameId)) throw new Exception($"{EmulatorName} 게임 식별자를 찾을 수 없습니다.");

                var (retroArchDir, command) = GetRetroArchInfo(game) ?? throw new NotSupportedException("RetroArch 에뮬레이터만 지원합니다.");
                var coreFolderName = ExtractCoreFolderName(command.Name);
                var folderId = await GetOrCreateFolderPathAsync(coreFolderName);

                var saveFiles = FindSaveFilePaths(game, gameId, mode);

                if (saveFiles == null || saveFiles.Length == 0)
                    throw new FileNotFoundException("세이브 파일을 찾을 수 없습니다.");

                var zipData = CompressFiles(saveFiles);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                var modePrefix = mode switch
                {
                    SaveBackupMode.SaveState => "STATE",
                    SaveBackupMode.Both => "BOTH",
                    _ => "SAVE"
                };

                var fileName = $"{gameId}_{modePrefix}_{timestamp}.zip";
                var fileId = await _driveService.UploadFileAsync(fileName, zipData, folderId);

                return fileId != null;
            }
            catch (Exception ex) when (ex is not FileNotFoundException && ex is not NotSupportedException && ex is not IOException)
            {
                throw new IOException($"백업 중 오류가 발생했습니다: {ex.Message}", ex);
            }
        }

        public override async Task<List<SaveBackupInfo>> GetBackupListAsync(GameMetadata game, int limit = 20)
        {
            try
            {
                var retroArchInfo = GetRetroArchInfo(game);
                if (retroArchInfo == null) return [];

                var (_, command) = retroArchInfo.Value;
                var coreFolderName = ExtractCoreFolderName(command.Name);
                var folderId = await GetOrCreateFolderPathAsync(coreFolderName);
                var romFileName = Path.GetFileNameWithoutExtension(game.RomFile);
                var allBackups = await _driveService.FindFilesByPrefixAsync(romFileName, folderId, limit);

                var backups = allBackups
                    .Select(b => new SaveBackupInfo
                    {
                        FileId = b.FileId,
                        FileName = b.FileName,
                        ModifiedTime = b.ModifiedTime,
                        Mode = ParseModeFromFileName(b.FileName)
                    })
                    .OrderByDescending(b => b.ModifiedTime)
                    .Take(limit)
                    .ToList();

                return backups;
            }
            catch
            {
                return [];
            }
        }

        public override async Task<bool> RestoreSaveAsync(GameMetadata game, string fileId)
        {
            try
            {
                var zipData = await _driveService.DownloadFileAsync(fileId);
                if (zipData == null) return false;

                var retroArchInfo = GetRetroArchInfo(game);
                if (retroArchInfo == null) return false;

                var (retroArchDir, command) = retroArchInfo.Value;
                var coreFolderName = ExtractCoreFolderName(command.Name);
                var folderId = await GetOrCreateFolderPathAsync(coreFolderName);
                var romFileName = Path.GetFileNameWithoutExtension(game.RomFile);
                var allBackups = await _driveService.FindFilesByPrefixAsync(romFileName, folderId, 100);
                var backup = allBackups.FirstOrDefault(b => b.FileId == fileId);

                if (backup == null) return false;

                var mode = ParseModeFromFileName(backup.FileName);
                ExtractSaveFilesByMode(zipData, retroArchDir, coreFolderName, command.CoreName, mode);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override async Task<bool> RestoreSaveAsync(GameMetadata game, SaveBackupMode? mode = null)
        {
            var backups = await GetBackupListAsync(game, 1);

            if (backups.Count == 0) return false;

            return await RestoreSaveAsync(game, backups[0].FileId);
        }

        public override async Task<bool> HasBackupAsync(GameMetadata game, SaveBackupMode? mode = null)
        {
            try
            {
                var retroArchInfo = GetRetroArchInfo(game);

                if (retroArchInfo == null) return false;

                var backups = await GetBackupListAsync(game, 1);

                return backups.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        protected async Task<string> GetOrCreateFolderPathAsync(string coreFolderName)
        {
            if (_ultimateEndFolderId == null)
            {
                _ultimateEndFolderId = await _driveService.FindFileByNameAsync("UltimateEnd", null);
                _ultimateEndFolderId ??= await _driveService.CreateFolderAsync("UltimateEnd", null);
            }

            if (_retroArchFolderId == null)
            {
                _retroArchFolderId = await _driveService.FindFileByNameAsync("RetroArch", _ultimateEndFolderId);
                _retroArchFolderId ??= await _driveService.CreateFolderAsync("RetroArch", _ultimateEndFolderId);
            }

            if (_savesFolderId == null)
            {
                _savesFolderId = await _driveService.FindFileByNameAsync("saves", _retroArchFolderId);
                _savesFolderId ??= await _driveService.CreateFolderAsync("saves", _retroArchFolderId);
            }

            if (_persistentFolderCache != null && _persistentFolderCache.TryGetValue(coreFolderName, out var cachedId)) return cachedId;

            var coreId = await _driveService.FindFileByNameAsync(coreFolderName, _savesFolderId);
            coreId ??= await _driveService.CreateFolderAsync(coreFolderName, _savesFolderId);

            if (_persistentFolderCache != null) _persistentFolderCache[coreFolderName] = coreId;

            return coreId;
        }

        private static void ExtractSaveFilesByMode(byte[] zipData, string retroArchDir, string coreFolderName, string coreName, SaveBackupMode mode)
        {
            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                var fileName = entry.Name;
                string? destinationPath = null;

                if (fileName.Contains(".state"))
                {
                    if (mode == SaveBackupMode.SaveState || mode == SaveBackupMode.Both)
                    {
                        var statesFolder = Path.Combine(retroArchDir, "states", coreFolderName);
                        Directory.CreateDirectory(statesFolder);
                        destinationPath = Path.Combine(statesFolder, fileName);
                    }
                }
                else
                {
                    if (mode == SaveBackupMode.NormalSave || mode == SaveBackupMode.Both)
                    {
                        var saveInfo = RetroArchSaveConfig.GetSaveInfo(coreName);
                        var savesFolder = Path.Combine(retroArchDir, "saves", coreFolderName);

                        if (!string.IsNullOrEmpty(saveInfo.SubFolder)) savesFolder = Path.Combine(savesFolder, saveInfo.SubFolder);

                        Directory.CreateDirectory(savesFolder);
                        destinationPath = Path.Combine(savesFolder, fileName);
                    }
                }

                if (destinationPath != null)
                {
                    BackupExistingFile(destinationPath);
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        protected static string ExtractCoreFolderName(string commandName)
        {
            var start = commandName.IndexOf('(');
            var end = commandName.IndexOf(')');

            if (start >= 0 && end > start) return commandName.Substring(start + 1, end - start - 1).Trim();

            return commandName;
        }

        public static IEmulatorCommand GetEmulatorCommand(string platformId, string? emulatorId = null)
        {
            var service = (CommandConfigServiceFactory.Create?.Invoke()) ?? throw new InvalidOperationException("CommandConfigService not initialized.");
            var config = service.LoadConfig();

            if (!string.IsNullOrEmpty(emulatorId))
            {
                if (config.Emulators.TryGetValue(emulatorId, out var specifiedEmulator))
                {
                    if (specifiedEmulator is IEmulatorCommand specifiedCommand) return specifiedCommand;

                    throw new InvalidOperationException("잘못된 에뮬레이터 명령 타입입니다.");
                }

                throw new InvalidOperationException($"에뮬레이터 '{emulatorId}'를 설정에서 찾을 수 없습니다.");
            }

            var normalizedPlatformId = PlatformInfoService.NormalizePlatformId(platformId);

            if (config.DefaultEmulators.TryGetValue(normalizedPlatformId, out string? defaultEmulatorId))
            {
                if (!config.Emulators.ContainsKey(defaultEmulatorId)) defaultEmulatorId = null;
            }

            if (string.IsNullOrEmpty(defaultEmulatorId))
            {
                var supportedEmulators = config.Emulators.Values
                    .Where(e => e.SupportedPlatforms
                    .Select(p => PlatformInfoService.NormalizePlatformId(p))
                    .Contains(normalizedPlatformId))
                    .OrderBy(e => e.Name.Contains("RetroArch", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                    .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                defaultEmulatorId = supportedEmulators.FirstOrDefault()?.Id;
            }

            if (string.IsNullOrEmpty(defaultEmulatorId)) throw new NotSupportedException($"'{platformId}' 플랫폼을 지원하는 에뮬레이터가 없습니다.");

            if (!config.Emulators.TryGetValue(defaultEmulatorId, out var emulatorCommand)) throw new InvalidOperationException($"에뮬레이터 '{defaultEmulatorId}'를 설정에서 찾을 수 없습니다.");

            if (emulatorCommand is not IEmulatorCommand command) throw new InvalidOperationException("잘못된 에뮬레이터 명령 타입입니다.");

            return command;
        }
    }
}