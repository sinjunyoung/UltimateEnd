using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using System;
using System.IO;
using UltimateEnd.Models;

namespace UltimateEnd.SaveFile
{
    public static class SwitchTitleIdExtractor
    {
        private static KeySet? _keySetCache;
        private static string? _keysPath;

        /// <summary>
        /// prod.keys 파일 경로를 설정합니다. (플랫폼별로 호출 필요)
        /// </summary>
        public static void SetKeysPath(string keysPath)
        {
            _keysPath = keysPath;
            _keySetCache = null;
        }

        private static KeySet LoadKeySet()
        {
            if (_keySetCache != null)
                return _keySetCache;

            var keySet = new KeySet();

            if (!string.IsNullOrEmpty(_keysPath) && File.Exists(_keysPath))
            {
                try
                {
                    var keysText = File.ReadAllText(_keysPath);
                    ExternalKeyReader.ReadKeyFile(keySet, filename: _keysPath);
                    System.Diagnostics.Debug.WriteLine($"[Eden] prod.keys 로드 성공: {_keysPath}");
                    _keySetCache = keySet;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Eden] prod.keys 로드 실패: {ex.Message}");
                }
            }

            return keySet;
        }

        public static string? GetTitleId(GameMetadata game)
        {
            string fullPath = game?.GetRomFullPath();

            if (fullPath == null || !File.Exists(fullPath)) return null;

            var extension = System.IO.Path.GetExtension(fullPath).ToLowerInvariant();

            try
            {
                return extension switch
                {
                    ".nsp" => ExtractFromNSP(fullPath),
                    ".xci" => ExtractFromXCI(fullPath),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Eden] Title ID 추출 실패: {ex.Message}");
                return null;
            }
        }

        private static string? ExtractFromNSP(string filePath)
        {
            try
            {
                using var storage = new LocalStorage(filePath, FileAccess.Read);

                var pfs = new PartitionFileSystem();
                pfs.Initialize(storage).ThrowIfFailure();

                foreach (var entry in pfs.EnumerateEntries("/", "*"))
                {
                    if (entry.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase))
                    {
                        using var ncaStorage = new UniqueRef<IFile>();
                        pfs.OpenFile(ref ncaStorage.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                        var nca = new Nca(LoadKeySet(), ncaStorage.Get.AsStorage());

                        var titleId = nca.Header.TitleId;
                        var titleIdStr = titleId.ToString("X16");

                        System.Diagnostics.Debug.WriteLine($"[Eden] ✓ Title ID (NSP): {titleIdStr}");
                        return titleIdStr;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Eden] NSP 오류: {ex.Message}");
            }

            return null;
        }

        private static string? ExtractFromXCI(string filePath)
        {
            try
            {
                using var storage = new LocalStorage(filePath, FileAccess.Read);

                var xci = new Xci(LoadKeySet(), storage);

                if (xci.HasPartition(XciPartitionType.Secure))
                {
                    var secure = xci.OpenPartition(XciPartitionType.Secure);

                    foreach (var entry in secure.EnumerateEntries("/", "*"))
                    {
                        if (entry.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase))
                        {
                            using var ncaStorage = new UniqueRef<IFile>();
                            secure.OpenFile(ref ncaStorage.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                            var nca = new Nca(LoadKeySet(), ncaStorage.Get.AsStorage());

                            var titleId = nca.Header.TitleId;
                            var titleIdStr = titleId.ToString("X16");

                            System.Diagnostics.Debug.WriteLine($"[Eden] ✓ Title ID (XCI): {titleIdStr}");
                            return titleIdStr;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Eden] XCI 오류: {ex.Message}");
            }

            return null;
        }
    }
}