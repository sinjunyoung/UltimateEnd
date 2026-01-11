using System;
using System.IO;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Android.SaveFile
{
    public class GameCubeSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command, string? packageName = null) : UltimateEnd.SaveFile.Dolphin.SaveBackupServiceBase(driveService, command)
    {
        private readonly string _packageName = packageName ?? "org.dolphinemu.dolphinemu";

        protected override string GetEmulatorBasePath(IEmulatorCommand command)
        {
            var externalStorage = global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;

            if (string.IsNullOrEmpty(externalStorage)) throw new InvalidOperationException("외부 저장소를 찾을 수 없습니다.");

            var path = Path.Combine(externalStorage, "Android", "data", _packageName, "files");

            if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Dolphin 데이터 폴더가 존재하지 않습니다.\n경로: {path}");

            return path;
        }
    }
}