using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace UltimateEnd.Services
{
    public class PlatformMappingService
    {
        private static readonly Lazy<PlatformMappingService> _instance = new(() => new PlatformMappingService());
        private const string SettingsFileName = "platform_mappings.json";
        private PlatformMappingConfig? _config;
        private readonly object _configLock = new();

        private readonly IPathConverter? _pathConverter = PathConverterFactory.Create?.Invoke();

        public static PlatformMappingService Instance => _instance.Value;

        private PlatformMappingService() { }

        private static string GetConfigPath()
        {
            var provider = AppBaseFolderProviderFactory.Create?.Invoke();
            if (provider != null)
                return Path.Combine(provider.GetSettingsFolder(), SettingsFileName);

            return Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        }

        public PlatformMappingConfig LoadMapping()
        {
            lock (_configLock)
            {
                if (_config != null) return _config;

                var configPath = GetConfigPath();
                if (!File.Exists(configPath))
                {
                    _config = new PlatformMappingConfig();
                    return _config;
                }

                try
                {
                    var json = File.ReadAllText(configPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };

                    var loadedConfig = JsonSerializer.Deserialize<PlatformMappingConfig>(json, options)
                        ?? new PlatformMappingConfig();

                    _config = new PlatformMappingConfig
                    {
                        FolderMappings = loadedConfig.FolderMappings
                            .ToDictionary(
                                kvp => _pathConverter?.FriendlyPathToRealPath(kvp.Key) ?? kvp.Key,
                                kvp => kvp.Value,
                                StringComparer.OrdinalIgnoreCase
                            ),
                        CustomDisplayNames = loadedConfig.CustomDisplayNames
                    };

                    return _config;
                }
                catch
                {
                    _config = new PlatformMappingConfig();
                    return _config;
                }
            }
        }

        public void SaveMapping(PlatformMappingConfig config)
        {
            lock (_configLock)
            {
                var configPath = GetConfigPath();
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                var json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(configPath, json);
                _config = config;
            }
        }

        public void ClearCache()
        {
            lock (_configLock)
                _config = null;
        }

        public string? GetMappedPlatformId(string folderPath)
        {
            var config = LoadMapping();

            if (config.FolderMappings.TryGetValue(folderPath, out var platformId))
                return platformId;

            return null;
        }

        public void AddMapping(string friendlyPath, string platformId)
        {
            lock (_configLock)
            {
                var config = LoadMapping();

                var realPath = _pathConverter?.FriendlyPathToRealPath(friendlyPath) ?? friendlyPath;
                config.FolderMappings[realPath] = platformId;

                var saveConfig = new PlatformMappingConfig
                {
                    FolderMappings = config.FolderMappings
                        .ToDictionary(
                            kvp => _pathConverter?.RealPathToFriendlyPath(kvp.Key) ?? kvp.Key,
                            kvp => kvp.Value,
                            StringComparer.OrdinalIgnoreCase
                        ),
                    CustomDisplayNames = config.CustomDisplayNames
                };

                SaveMapping(saveConfig);
            }
        }

        public void RemoveMapping(string friendlyPath)
        {
            lock (_configLock)
            {
                var config = LoadMapping();
                var realPath = _pathConverter?.FriendlyPathToRealPath(friendlyPath) ?? friendlyPath;
                config.FolderMappings.Remove(realPath);

                var saveConfig = new PlatformMappingConfig
                {
                    FolderMappings = config.FolderMappings
                        .ToDictionary(
                            kvp => _pathConverter?.RealPathToFriendlyPath(kvp.Key) ?? kvp.Key,
                            kvp => kvp.Value,
                            StringComparer.OrdinalIgnoreCase
                        ),
                    CustomDisplayNames = config.CustomDisplayNames
                };

                SaveMapping(saveConfig);
            }
        }

        public bool HasMapping(string folderPath)
        {
            return GetMappedPlatformId(folderPath) != null;
        }

        public Dictionary<string, string> GetAllMappings()
        {
            var config = LoadMapping();
            return new Dictionary<string, string>(config.FolderMappings, StringComparer.OrdinalIgnoreCase);
        }
    }
}