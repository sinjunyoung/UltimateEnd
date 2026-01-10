using System;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Android.Utils;
using UltimateEnd.Models;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Android.SaveFile
{
    public class SwitchSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command, IFolderPicker folderPicker, string? defaultPackageName = null) : EdenSaveBackupServiceBase(driveService, command)
    {
        private const string CONFIG_FILE_NAME = "eden_path_config.txt";
        private static string? _cachedPath;
        private readonly IFolderPicker _folderPicker = folderPicker;
        private readonly string _defaultPackageName = defaultPackageName ?? "dev.eden.eden_emulator";

        protected override string GetEdenBasePath(IEmulatorCommand command)
        {
            if (!string.IsNullOrEmpty(_cachedPath) && IsValidEdenPath(_cachedPath)) return _cachedPath;

            var savedPath = ReadSavedPath();

            if (!string.IsNullOrEmpty(savedPath) && IsValidEdenPath(savedPath))
            {
                _cachedPath = savedPath;
                return _cachedPath;
            }

            var defaultPath = GetDefaultEdenPath();

            if (!string.IsNullOrEmpty(defaultPath) && IsValidEdenPath(defaultPath))
            {
                _cachedPath = defaultPath;
                SavePath(defaultPath);
                return _cachedPath;
            }

            throw new InvalidOperationException("Eden 경로가 설정되지 않았습니다.");
        }

        protected override string? GetProKeysPath()
        {
            try
            {
                var basePath = GetEdenBasePath(_command);
                return Path.Combine(basePath, "keys", "prod.keys");
            }
            catch
            {
                return null;
            }
        }

        private string? GetDefaultEdenPath()
        {
            try
            {
                var externalStorage = global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;

                if (string.IsNullOrEmpty(externalStorage)) return null;

                return Path.Combine(externalStorage, "Android", "data", _defaultPackageName, "files");
            }
            catch
            {
                return null;
            }
        }

        public override async Task<bool> BackupSaveAsync(GameMetadata game, SaveBackupMode mode = SaveBackupMode.NormalSave)
        {
            try
            {
                if (!await EnsurePathConfiguredAsync()) return false;

                return await base.BackupSaveAsync(game, mode);
            }
            catch (InvalidOperationException) when (GetCurrentPath() == null)
            {
                return await HandlePathError("Eden 경로가 설정되지 않았습니다.", () => BackupSaveAsync(game, mode));
            }
            catch (DirectoryNotFoundException)
            {
                return await HandlePathError("저장된 Eden 경로를 찾을 수 없습니다.\n폴더가 삭제되었거나 접근할 수 없습니다.", () => BackupSaveAsync(game, mode));
            }
        }

        public override async Task<bool> RestoreSaveAsync(GameMetadata game, string fileId)
        {
            try
            {
                if (!await EnsurePathConfiguredAsync()) return false;

                return await base.RestoreSaveAsync(game, fileId);
            }
            catch (InvalidOperationException) when (GetCurrentPath() == null)
            {
                return await HandlePathError("Eden 경로가 설정되지 않았습니다.", () => RestoreSaveAsync(game, fileId));
            }
            catch (DirectoryNotFoundException)
            {
                return await HandlePathError("저장된 Eden 경로를 찾을 수 없습니다.\n폴더가 삭제되었거나 접근할 수 없습니다.", () => RestoreSaveAsync(game, fileId));
            }
        }

        public override async Task<bool> RestoreSaveAsync(GameMetadata game, SaveBackupMode? mode = null)
        {
            try
            {
                if (!await EnsurePathConfiguredAsync()) return false;

                return await base.RestoreSaveAsync(game, mode);
            }
            catch (InvalidOperationException) when (GetCurrentPath() == null)
            {
                return await HandlePathError("Eden 경로가 설정되지 않았습니다.", () => RestoreSaveAsync(game, mode));
            }
            catch (DirectoryNotFoundException)
            {
                return await HandlePathError("저장된 Eden 경로를 찾을 수 없습니다.\n폴더가 삭제되었거나 접근할 수 없습니다.", () => RestoreSaveAsync(game, mode));
            }
        }

        private async Task<bool> HandlePathError(string errorMessage, Func<Task<bool>> retryAction)
        {
            _cachedPath = null;

            var retry = await AndroidDialogHelper.ShowErrorAndAskRetryAsync($"{errorMessage}\n\n경로를 다시 설정하시겠습니까?");

            if (retry)
            {
                var newPath = await RequestPathAsync();

                if (newPath != null) return await retryAction();
            }

            return false;
        }

        private async Task<bool> EnsurePathConfiguredAsync()
        {
            if (!string.IsNullOrEmpty(GetCurrentPath())) return true;

            return await RequestPathAsync() != null;
        }

        private async Task<string?> RequestPathAsync()
        {
            var selectedPath = await _folderPicker.PickFolderAsync("Eden 데이터 폴더 선택\n(nand/user/save 폴더가 있는 Eden 폴더)");

            if (string.IsNullOrEmpty(selectedPath)) return null;

            var edenPath = NormalizePath(selectedPath);

            if (IsValidEdenPath(edenPath))
            {
                SavePath(edenPath);
                await AndroidDialogHelper.ShowToastAsync("경로가 설정되었습니다.");

                return edenPath;
            }

            var retry = await AndroidDialogHelper.ShowErrorAndAskRetryAsync(
                "유효하지 않은 경로입니다.\n" +
                "nand/user/save 폴더가 있는 Eden 데이터 경로를 선택해주세요.\n\n" +
                "예시:\n" +
                "✓ /storage/emulated/0/Android/data/dev.eden.eden_emulator/files\n" +
                "✗ /storage/emulated/0/Android/data/dev.eden.eden_emulator/files/nand\n\n" +
                "다시 시도하시겠습니까?"
            );

            if (retry) return await RequestPathAsync();

            return null;
        }

        private static string NormalizePath(string selectedPath)
        {
            if (Directory.Exists(Path.Combine(selectedPath, "nand", "user", "save"))) return selectedPath;

            if (Directory.Exists(Path.Combine(selectedPath, "user", "save")))
            {
                var parent = Directory.GetParent(selectedPath);

                if (parent != null) return parent.FullName;
            }

            if (Directory.Exists(Path.Combine(selectedPath, "save")) && Path.GetFileName(selectedPath) == "user")
            {
                var parent = Directory.GetParent(selectedPath);

                if (parent != null && parent.Name == "nand")
                {
                    var grandParent = parent.Parent;

                    if (grandParent != null) return grandParent.FullName;
                }
            }

            if (Path.GetFileName(selectedPath) == "save")
            {
                var parent = Directory.GetParent(selectedPath);

                if (parent != null && parent.Name == "user")
                {
                    var grandParent = parent.Parent;

                    if (grandParent != null && grandParent.Name == "nand")
                    {
                        var greatGrandParent = grandParent.Parent;

                        if (greatGrandParent != null) return greatGrandParent.FullName;
                    }
                }
            }

            return selectedPath;
        }

        public static void ResetPath()
        {
            _cachedPath = null;
            var configPath = GetConfigFilePath();

            if (File.Exists(configPath))
            {
                try
                {
                    File.Delete(configPath);
                }
                catch { }
            }
        }

        public async Task<bool> ReconfigurePathAsync()
        {
            ResetPath();

            var newPath = await RequestPathAsync();

            return newPath != null;
        }

        public static string? GetCurrentPath()
        {
            if (!string.IsNullOrEmpty(_cachedPath) && IsValidEdenPath(_cachedPath)) return _cachedPath;

            var savedPath = ReadSavedPath();

            if (!string.IsNullOrEmpty(savedPath) && IsValidEdenPath(savedPath))
            {
                _cachedPath = savedPath;
                return _cachedPath;
            }

            return null;
        }

        private static bool IsValidEdenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                var savePath = Path.Combine(path, "nand", "user", "save");
                return Directory.Exists(savePath);
            }
            catch
            {
                return false;
            }
        }

        private static void SavePath(string path)
        {
            try
            {
                var configPath = GetConfigFilePath();
                var directory = Path.GetDirectoryName(configPath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(configPath, path);
                _cachedPath = path;
            }
            catch { }
        }

        private static string? ReadSavedPath()
        {
            try
            {
                var configPath = GetConfigFilePath();

                if (File.Exists(configPath))
                {
                    var path = File.ReadAllText(configPath).Trim();
                    return string.IsNullOrWhiteSpace(path) ? null : path;
                }
            }
            catch { }

            return null;
        }

        private static string GetConfigFilePath()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            return Path.Combine(appDataPath, CONFIG_FILE_NAME);
        }
    }
}