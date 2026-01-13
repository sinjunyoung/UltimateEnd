using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimateEnd.Services;

namespace UltimateEnd.Utils
{
    public static class PathHelper
    {
        private static List<string> _romsBasePaths = new();
        private static IPlatformStorageInfo? _storageInfo;

        public static void Initialize(IEnumerable<string> romsBasePaths)
        {
            if (romsBasePaths == null || !romsBasePaths.Any())
                throw new ArgumentNullException(nameof(romsBasePaths));

            _storageInfo = PlatformStorageInfoFactory.Create?.Invoke();
            _romsBasePaths = [.. romsBasePaths.Select(NormalizePath)];
        }

        public static void Initialize(string romsBasePath)
        {
            if (string.IsNullOrEmpty(romsBasePath))
                throw new ArgumentNullException(nameof(romsBasePath));

            Initialize([romsBasePath]);
        }

        public static string ToRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return absolutePath;

            if (_romsBasePaths.Count == 0)
                throw new InvalidOperationException("PathHelper가 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.");

            if (absolutePath.StartsWith("content://")) return absolutePath;

            try
            {
                var normalizedPath = NormalizePath(absolutePath);

                foreach (var basePath in _romsBasePaths)
                {
                    var normalizedBase = NormalizePath(basePath);

                    if (normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                    {
                        var relativePath = Path.GetRelativePath(normalizedBase, normalizedPath);

                        if (!Path.IsPathRooted(relativePath))
                        {
                            relativePath = relativePath.Replace('\\', '/');

                            if (!relativePath.StartsWith("../"))
                                relativePath = "./" + relativePath;

                            return relativePath;
                        }
                    }
                }
            }
            catch { }

            return absolutePath;
        }

        public static string ToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            if (_romsBasePaths.Count == 0)
                throw new InvalidOperationException("PathHelper가 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.");

            if (path.StartsWith("content://")) return path;

            if (Path.IsPathRooted(path)) return NormalizePath(path);

            if (IsRelativePath(path))
            {
                try
                {
                    var cleanPath = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

                    foreach (var basePath in _romsBasePaths)
                    {
                        var absolutePath = Path.GetFullPath(Path.Combine(basePath, cleanPath));

                        if (File.Exists(absolutePath) || Directory.Exists(absolutePath)) return NormalizePath(absolutePath);
                    }

                    var firstAttempt = Path.GetFullPath(Path.Combine(_romsBasePaths[0], cleanPath));

                    return NormalizePath(firstAttempt);
                }
                catch { }
            }

            return path;
        }

        private static bool IsRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            return path.StartsWith("./") || path.StartsWith(".\\") || path.StartsWith("../") || path.StartsWith("..\\") || path == "." || path == "..";
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            if (path.StartsWith("content://")) return path;

            var primaryStorage = _storageInfo?.GetPrimaryStoragePath();

            if (!string.IsNullOrEmpty(primaryStorage))
            {
                List<string> internalAliases =
                    [
                    "/sdcard/",
                    "/mnt/sdcard/",
                    "/storage/sdcard0/",
                    "/storage/emulated/legacy/",
                    "/data/media/0/"
                    ];

                foreach (var alias in internalAliases)
                {
                    if (path.StartsWith(alias, StringComparison.OrdinalIgnoreCase))
                    {
                        path = primaryStorage + "/" + path.Substring(alias.Length);
                        break;
                    }
                }
            }

            var externalSdCard = _storageInfo?.GetExternalSdCardPath();

            if (!string.IsNullOrEmpty(externalSdCard))
            {
                List<string> externalAliases =
                    [
                    "/mnt/extSdCard/",
                    "/mnt/external_sd/",
                    "/storage/sdcard1/"
                    ];

                foreach (var alias in externalAliases)
                {
                    if (path.StartsWith(alias, StringComparison.OrdinalIgnoreCase))
                    {
                        path = string.Concat(externalSdCard, "/", path.AsSpan(alias.Length));
                        break;
                    }
                }
            }

            try
            {
                bool isUncPath = new Uri(path).IsUnc;

                path = path.Replace("//", "/");

                if (!isUncPath)
                {
                    path = path.Replace("\\\\", "\\");
                }

                if (isUncPath || Path.IsPathRooted(path))
                {
                    return path;
                }

                path = Path.GetFullPath(path);
            }
            catch
            {
                path = path.TrimEnd('/', '\\');
            }

            return path;
        }

        public static bool ArePathsEqual(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1) && string.IsNullOrEmpty(path2)) return true;

            if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2)) return false;

            var normalized1 = NormalizePath(path1);
            var normalized2 = NormalizePath(path2);

            return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSubPathOf(string path, string? basePath = null)
        {
            if (string.IsNullOrEmpty(path)) return false;

            var normalizedPath = NormalizePath(path);
            
            if (normalizedPath.StartsWith("content://")) return false;

            if (!string.IsNullOrEmpty(basePath))
            {
                var normalizedBase = NormalizePath(basePath);

                return normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
            }

            if (_romsBasePaths.Count == 0) return false;

            return _romsBasePaths.Any(bp =>
            {
                var normalizedBase = NormalizePath(bp);

                return normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            return sanitized.TrimStart('.');
        }

        public static List<string> GetBasePaths()
        {
            if (_romsBasePaths.Count == 0)
                throw new InvalidOperationException("PathHelper가 초기화되지 않았습니다.");

            return [.. _romsBasePaths];
        }

        public static string GetBasePath()
        {
            if (_romsBasePaths.Count == 0)
                throw new InvalidOperationException("PathHelper가 초기화되지 않았습니다.");

            return _romsBasePaths[0];
        }

        public static string? FindBasePathFor(string path)
        {
            if (string.IsNullOrEmpty(path) || _romsBasePaths.Count == 0) return null;

            var normalizedPath = NormalizePath(path);

            return _romsBasePaths
                .Select(NormalizePath)
                .FirstOrDefault(basePath => normalizedPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (path.StartsWith("content://"))
                return true;

            try
            {
                Path.GetFullPath(path);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}