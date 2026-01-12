using System;
using System.IO;
using UltimateEnd.SaveFile;
using UltimateEnd.Services;

namespace UltimateEnd.Android.SaveFile
{
    public class CemuSaveBackupService(GoogleDriveService driveService, IEmulatorCommand command, string? defaultPackageName = null) : UltimateEnd.SaveFile.Cemu.SaveBackupServiceBase(driveService, command)
    {
        private readonly string _packageName = defaultPackageName ?? "info.cemu.cemu";

        protected override string GetBasePath(IEmulatorCommand command)
        {
            var externalStorage = global::Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;

            if (string.IsNullOrEmpty(externalStorage)) throw new InvalidOperationException("외부 저장소를 찾을 수 없습니다.");

            var path = Path.Combine(externalStorage, "Android", "data", _packageName, "files");

            if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"에뮬레이터 데이터 폴더가 존재하지 않습니다.\n경로: {path}\n\n게임을 한 번 실행하여 폴더를 생성해주세요.");

            return path;
        }
    }
}