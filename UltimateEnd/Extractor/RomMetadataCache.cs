using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UltimateEnd.Services;

namespace UltimateEnd.Extractor
{
    public class RomMetadataCache
    {
        private readonly string _cacheDirectory;
        private const string MetadataFileName = "metadata.json";

        public RomMetadataCache(string platformId)
        {
            var factory = AppBaseFolderProviderFactory.Create.Invoke();            
            var baseCacheDir = Path.Combine(factory.GetPlatformsFolder(), platformId);

            _cacheDirectory = baseCacheDir;
            Directory.CreateDirectory(_cacheDirectory);
        }

        public static string GetTitleId(string romFilePath) => Path.GetFileNameWithoutExtension(romFilePath);

        public async Task<CachedMetadata> GetCachedMetadata(string romFilePath)
        {
            var titleId = GetTitleId(romFilePath);
            var metadataPath = Path.Combine(_cacheDirectory, titleId, MetadataFileName);

            if (!File.Exists(metadataPath)) return null;

            try
            {
                var json = await File.ReadAllTextAsync(metadataPath);
                var cached = JsonSerializer.Deserialize<CachedMetadata>(json);
                var fileInfo = new FileInfo(romFilePath);

                if (cached.FileSize != fileInfo.Length || cached.LastModified != fileInfo.LastWriteTime) return null; 

                if (!string.IsNullOrEmpty(cached.CoverImagePath) && !File.Exists(cached.CoverImagePath)) return null;

                return cached;
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveMetadata(string romFilePath, ExtractedMetadata metadata)
        {
            var titleId = GetTitleId(romFilePath);
            var titleDir = Path.Combine(_cacheDirectory, titleId);
            Directory.CreateDirectory(titleDir);
            var fileInfo = new FileInfo(romFilePath);

            var cached = new CachedMetadata
            {
                TitleId = titleId,
                Title = metadata.Title,
                Developer = metadata.Developer,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime
            };

            if (metadata.CoverImage != null)
            {
                var coverPath = Path.Combine(titleDir, "cover.png");
                await File.WriteAllBytesAsync(coverPath, metadata.CoverImage);
                cached.CoverImagePath = coverPath;
            }

            if (metadata.LogoImage != null)
            {
                var logoPath = Path.Combine(titleDir, "logo.png");
                await File.WriteAllBytesAsync(logoPath, metadata.LogoImage);
                cached.LogoImagePath = logoPath;
            }

            var metadataPath = Path.Combine(titleDir, MetadataFileName);
            var json = JsonSerializer.Serialize(cached, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await File.WriteAllTextAsync(metadataPath, json);
        }

        public void DeleteCache(string romFilePath)
        {
            var titleId = GetTitleId(romFilePath);
            var titleDir = Path.Combine(_cacheDirectory, titleId);

            if (Directory.Exists(titleDir)) Directory.Delete(titleDir, true);
        }

        public void ClearAllCache()
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, true);
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        public long GetCacheSize()
        {
            if (!Directory.Exists(_cacheDirectory)) return 0;

            long size = 0;
            var dirInfo = new DirectoryInfo(_cacheDirectory);

            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories)) size += file.Length;

            return size / 1024 / 1024;
        }
    }
}