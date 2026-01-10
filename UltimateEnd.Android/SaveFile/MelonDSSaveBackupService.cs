using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Android.SaveFile
{
    public class MelonDSSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command, IFolderPicker folderPicker) : SaveBackupServiceBase(driveService)
    {
        private const string SAV_CONFIG_FILE_NAME = "melonds_sav_path_config.txt";
        private const string STATE_CONFIG_FILE_NAME = "melonds_state_path_config.txt";

        private static string? _cachedSavPath;
        private static string? _cachedStatePath;

        private readonly IEmulatorCommand _command = command;
        private readonly IFolderPicker _folderPicker = folderPicker;

        protected override string EmulatorName => "MelonDS";

        protected override string? GetGameIdentifier(GameMetadata game) => string.IsNullOrEmpty(game.RomFile) ? null : Path.GetFileNameWithoutExtension(game.RomFile);

        protected override string[] FindSaveFilePaths(GameMetadata game, string gameId, SaveBackupMode mode)
        {
            if (mode == SaveBackupMode.NormalSave)
            {
                var savPath = GetMelonDSSavePath();

                if (string.IsNullOrEmpty(savPath) || !Directory.Exists(savPath)) return [];

                var saveFile = Path.Combine(savPath, $"{gameId}.sav");

                return File.Exists(saveFile) ? [saveFile] : [];
            }
            else if (mode == SaveBackupMode.SaveState)
            {
                var statePath = GetMelonDSStatePath();

                if (string.IsNullOrEmpty(statePath) || !Directory.Exists(statePath)) return [];

                return [.. Enumerable.Range(1, 8)
                    .Select(i => Path.Combine(statePath, $"{gameId}.ml{i}"))
                    .Where(File.Exists)];
            }

            return [];
        }

        protected override void RestoreSaveFilesToDisk(byte[] zipData, GameMetadata game, string gameId, SaveBackupMode mode)
        {
            string targetPath;

            if (mode == SaveBackupMode.NormalSave)
            {
                targetPath = GetMelonDSSavePath();

                if (string.IsNullOrEmpty(targetPath)) throw new InvalidOperationException("MelonDS 일반 세이브 경로를 찾을 수 없습니다.");
            }
            else
            {
                targetPath = GetMelonDSStatePath();

                if (string.IsNullOrEmpty(targetPath)) throw new InvalidOperationException("MelonDS 스테이트 세이브 경로를 찾을 수 없습니다.");
            }

            Directory.CreateDirectory(targetPath);

            using var memoryStream = new MemoryStream(zipData);
            using var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read);

            if (mode == SaveBackupMode.NormalSave)
            {
                var saveFile = Path.Combine(targetPath, $"{gameId}.sav");
                BackupExistingFile(saveFile);

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var destinationPath = Path.Combine(targetPath, entry.Name);
                    entry.ExtractToFile(destinationPath, true);
                }
            }
            else if (mode == SaveBackupMode.SaveState)
            {
                for (int i = 1; i <= 8; i++)
                {
                    var stateFile = Path.Combine(targetPath, $"{gameId}.ml{i}");
                    BackupExistingFile(stateFile);
                }

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    var destinationPath = Path.Combine(targetPath, entry.Name);
                    entry.ExtractToFile(destinationPath, true);
                }
            }
        }

        #region 경로 설정 관련 메서드 (MelonDS 특화)

        public override async Task<bool> BackupSaveAsync(GameMetadata game, SaveBackupMode mode = SaveBackupMode.NormalSave)
        {
            try
            {
                if (mode == SaveBackupMode.NormalSave || mode == SaveBackupMode.Both)
                    if (!await EnsurePathConfiguredAsync(SaveBackupMode.NormalSave)) return false;

                if (mode == SaveBackupMode.SaveState || mode == SaveBackupMode.Both)
                    if (!await EnsurePathConfiguredAsync(SaveBackupMode.SaveState)) return false;

                return await base.BackupSaveAsync(game, mode);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("일반 세이브"))
            {
                return await HandlePathError("MelonDS 일반 세이브(.sav) 경로가 설정되지 않았습니다.", SaveBackupMode.NormalSave, () => BackupSaveAsync(game, mode));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("스테이트"))
            {
                return await HandlePathError("MelonDS 스테이트 세이브(.ml) 경로가 설정되지 않았습니다.", SaveBackupMode.SaveState, () => BackupSaveAsync(game, mode));
            }
            catch (DirectoryNotFoundException)
            {
                _cachedSavPath = null;
                _cachedStatePath = null;
                return await HandlePathError("저장된 MelonDS 경로를 찾을 수 없습니다.\n폴더가 삭제되었거나 접근할 수 없습니다.", mode, () => BackupSaveAsync(game, mode));
            }
        }

        public override async Task<bool> RestoreSaveAsync(GameMetadata game, string fileId)
        {
            try
            {
                var backups = await GetBackupListAsync(game, 100);
                var backup = backups.FirstOrDefault(b => b.FileId == fileId);
                var mode = backup?.Mode ?? SaveBackupMode.NormalSave;

                if (!await EnsurePathConfiguredAsync(mode)) return false;

                return await base.RestoreSaveAsync(game, fileId);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("일반 세이브"))
            {
                return await HandlePathError("MelonDS 일반 세이브(.sav) 경로가 설정되지 않았습니다.", SaveBackupMode.NormalSave, () => RestoreSaveAsync(game, fileId));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("스테이트"))
            {
                return await HandlePathError("MelonDS 스테이트 세이브(.ml) 경로가 설정되지 않았습니다.", SaveBackupMode.SaveState, () => RestoreSaveAsync(game, fileId));
            }
            catch (DirectoryNotFoundException)
            {
                _cachedSavPath = null;
                _cachedStatePath = null;
                return await HandlePathError("저장된 MelonDS 경로를 찾을 수 없습니다.\n폴더가 삭제되었거나 접근할 수 없습니다.",SaveBackupMode.NormalSave, () => RestoreSaveAsync(game, fileId));
            }
        }

        private async Task<bool> HandlePathError(string errorMessage, SaveBackupMode mode, Func<Task<bool>> retryAction)
        {
            if (mode == SaveBackupMode.NormalSave) _cachedSavPath = null;
            else if (mode == SaveBackupMode.SaveState) _cachedStatePath = null;
            else
            {
                _cachedSavPath = null;
                _cachedStatePath = null;
            }

            var retry = await ShowErrorAndAskRetry($"{errorMessage}\n\n경로를 다시 설정하시겠습니까?");

            if (retry)
            {
                var newPath = await RequestPathAsync(mode);

                if (newPath != null) return await retryAction();
            }

            return false;
        }

        private async Task<bool> EnsurePathConfiguredAsync(SaveBackupMode mode)
        {
            if (mode == SaveBackupMode.NormalSave)
            {
                if (!string.IsNullOrEmpty(GetCurrentSavPath())) return true;
                return await RequestPathAsync(SaveBackupMode.NormalSave) != null;
            }
            else if (mode == SaveBackupMode.SaveState)
            {
                if (!string.IsNullOrEmpty(GetCurrentStatePath())) return true;
                return await RequestPathAsync(SaveBackupMode.SaveState) != null;
            }
            else
            {
                var savPathOk = !string.IsNullOrEmpty(GetCurrentSavPath()) || await RequestPathAsync(SaveBackupMode.NormalSave) != null;
                var statePathOk = !string.IsNullOrEmpty(GetCurrentStatePath()) || await RequestPathAsync(SaveBackupMode.SaveState) != null;
                return savPathOk && statePathOk;
            }
        }

        private async Task<string?> RequestPathAsync(SaveBackupMode mode)
        {
            var pathType = mode == SaveBackupMode.NormalSave ? "일반 세이브(.sav)" : "스테이트 세이브(.ml)";
            var defaultPath = mode == SaveBackupMode.NormalSave ? "/storage/emulated/0/Android/data/me.magnum.melonds/files/saves" : "/storage/emulated/0/Android/data/me.magnum.melonds/files/saves";
            var selectedPath = await _folderPicker.PickFolderAsync($"MelonDS {pathType} 폴더 선택\n기본 경로: {defaultPath}", defaultPath);

            if (string.IsNullOrEmpty(selectedPath)) return null;

            if (IsValidPath(selectedPath))
            {
                SavePath(selectedPath, mode);
                await ShowSuccessMessage($"{pathType} 경로가 설정되었습니다.");
                return selectedPath;
            }

            var retry = await ShowErrorAndAskRetry(
                $"유효하지 않은 경로입니다.\n" +
                $"{pathType} 파일이 저장되는 폴더를 선택해주세요.\n\n" +
                $"예시:\n" +
                $"✓ /storage/emulated/0/Android/data/me.magnum.melonds/files/saves\n" +
                $"✗ /storage/emulated/0/Android/data/me.magnum.melonds/files\n\n" +
                "다시 시도하시겠습니까?"
            );

            if (retry) return await RequestPathAsync(mode);

            return null;
        }

        private static string GetMelonDSSavePath()
        {
            if (!string.IsNullOrEmpty(_cachedSavPath) && IsValidPath(_cachedSavPath)) return _cachedSavPath;

            var savedPath = ReadSavedPath(SAV_CONFIG_FILE_NAME);

            if (!string.IsNullOrEmpty(savedPath) && IsValidPath(savedPath))
            {
                _cachedSavPath = savedPath;
                return _cachedSavPath;
            }

            throw new InvalidOperationException("MelonDS 일반 세이브 경로가 설정되지 않았습니다.");
        }

        private static string GetMelonDSStatePath()
        {
            if (!string.IsNullOrEmpty(_cachedStatePath) && IsValidPath(_cachedStatePath)) return _cachedStatePath;

            var savedPath = ReadSavedPath(STATE_CONFIG_FILE_NAME);

            if (!string.IsNullOrEmpty(savedPath) && IsValidPath(savedPath))
            {
                _cachedStatePath = savedPath;
                return _cachedStatePath;
            }

            throw new InvalidOperationException("MelonDS 스테이트 세이브 경로가 설정되지 않았습니다.");
        }

        public static void ResetPath(SaveBackupMode? mode = null)
        {
            if (mode == SaveBackupMode.NormalSave || mode == null)
            {
                _cachedSavPath = null;
                var configPath = GetConfigFilePath(SAV_CONFIG_FILE_NAME);

                if (File.Exists(configPath))
                {
                    try { File.Delete(configPath); }
                    catch { }
                }
            }

            if (mode == SaveBackupMode.SaveState || mode == null)
            {
                _cachedStatePath = null;
                var configPath = GetConfigFilePath(STATE_CONFIG_FILE_NAME);

                if (File.Exists(configPath))
                {
                    try { File.Delete(configPath); }
                    catch { }
                }
            }
        }

        public async Task<bool> ReconfigurePathAsync(SaveBackupMode mode)
        {
            ResetPath(mode);
            var newPath = await RequestPathAsync(mode);
            return newPath != null;
        }

        public async Task<bool> ReconfigureAllPathsAsync()
        {
            ResetPath();
            var savPathOk = await RequestPathAsync(SaveBackupMode.NormalSave) != null;
            var statePathOk = await RequestPathAsync(SaveBackupMode.SaveState) != null;
            return savPathOk && statePathOk;
        }

        public static string? GetCurrentSavPath()
        {
            if (!string.IsNullOrEmpty(_cachedSavPath) && IsValidPath(_cachedSavPath))
                return _cachedSavPath;

            var savedPath = ReadSavedPath(SAV_CONFIG_FILE_NAME);

            if (!string.IsNullOrEmpty(savedPath) && IsValidPath(savedPath))
            {
                _cachedSavPath = savedPath;
                return _cachedSavPath;
            }

            return null;
        }

        public static string? GetCurrentStatePath()
        {
            if (!string.IsNullOrEmpty(_cachedStatePath) && IsValidPath(_cachedStatePath))
                return _cachedStatePath;

            var savedPath = ReadSavedPath(STATE_CONFIG_FILE_NAME);

            if (!string.IsNullOrEmpty(savedPath) && IsValidPath(savedPath))
            {
                _cachedStatePath = savedPath;
                return _cachedStatePath;
            }

            return null;
        }

        private static bool IsValidPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static void SavePath(string path, SaveBackupMode mode)
        {
            try
            {
                var configFileName = mode == SaveBackupMode.NormalSave ? SAV_CONFIG_FILE_NAME : STATE_CONFIG_FILE_NAME;

                var configPath = GetConfigFilePath(configFileName);
                var directory = Path.GetDirectoryName(configPath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(configPath, path);

                if (mode == SaveBackupMode.NormalSave) _cachedSavPath = path;
                else _cachedStatePath = path;
            }
            catch { }
        }

        private static string? ReadSavedPath(string configFileName)
        {
            try
            {
                var configPath = GetConfigFilePath(configFileName);

                if (File.Exists(configPath))
                {
                    var path = File.ReadAllText(configPath).Trim();
                    return string.IsNullOrWhiteSpace(path) ? null : path;
                }
            }
            catch { }

            return null;
        }

        private static string GetConfigFilePath(string fileName)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(appDataPath, fileName);
        }

        private static Task ShowSuccessMessage(string message)
        {
            try
            {
                var activity = MainActivity.Instance;
                activity?.RunOnUiThread(() =>
                {
                    global::Android.Widget.Toast.MakeText(
                        activity,
                        message,
                        global::Android.Widget.ToastLength.Short
                    )?.Show();
                });
            }
            catch { }

            return Task.CompletedTask;
        }

        private static async Task<bool> ShowErrorAndAskRetry(string message)
        {
            try
            {
                var activity = MainActivity.Instance;

                if (activity == null) return false;

                var tcs = new TaskCompletionSource<bool>();

                await activity.RunOnUiThreadAsync(() =>
                {
                    var builder = new global::Android.App.AlertDialog.Builder(activity);
                    builder.SetTitle("오류");
                    builder.SetMessage(message);
                    builder.SetPositiveButton("재시도", (s, e) => tcs.TrySetResult(true));
                    builder.SetNegativeButton("취소", (s, e) => tcs.TrySetResult(false));
                    builder.SetCancelable(false);
                    builder.Show();
                });

                return await tcs.Task;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}