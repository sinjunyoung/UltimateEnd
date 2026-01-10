using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Managers;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public class EmulatorConfigService
    {
        public static List<EmulatorInfo> GetAvailableEmulators(string platformId, string? selectedGamePlatformId = null)
        {
            var service = CommandConfigServiceFactory.Create?.Invoke();

            if (service == null) return [];

            ICommandConfig config = service.LoadConfig();

            var targetPlatformId = GameMetadataManager.IsSpecialPlatform(platformId) ? (selectedGamePlatformId ?? platformId) : platformId;
            var normalizedPlatformId = PlatformInfoService.NormalizePlatformId(targetPlatformId);
            var emulators = CollectEmulators(config, normalizedPlatformId);

            SortEmulators(emulators);

            return emulators;
        }

        private static List<EmulatorInfo> CollectEmulators(ICommandConfig config, string platformId)
        {
            var emulators = new List<EmulatorInfo>();

            foreach (IEmulatorCommand emulator in config.Emulators.Values)
            {
                var normalizedSupportedPlatforms = emulator.SupportedPlatforms
                    .Select(p => PlatformInfoService.NormalizePlatformId(p))
                    .ToList();

                if (normalizedSupportedPlatforms.Contains(platformId))
                {
                    bool isDefault = config.DefaultEmulators.TryGetValue(platformId, out var defaultId) && defaultId == emulator.Id;

                    emulators.Add(new EmulatorInfo
                    {
                        Id = emulator.Id,
                        Name = emulator.Name,
                        Icon = emulator.Icon,
                        IsDefault = isDefault
                    });
                }
            }

            return emulators;
        }

        private static void SortEmulators(List<EmulatorInfo> emulators)
        {
            emulators.Sort((a, b) =>
            {
                bool aIsRetroArch = a.Name.Contains("retroarch", StringComparison.OrdinalIgnoreCase);
                bool bIsRetroArch = b.Name.Contains("retroarch", StringComparison.OrdinalIgnoreCase);

                if (aIsRetroArch && !bIsRetroArch) return -1;
                if (!aIsRetroArch && bIsRetroArch) return 1;

                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        public static void SetEmulator(string platformId, string emulatorId, string? selectedGamePlatformId = null)
        {
            var service = CommandConfigServiceFactory.Create?.Invoke();

            if (service == null) return;

            var config = service.LoadConfig();
            var targetPlatformId = GameMetadataManager.IsSpecialPlatform(platformId) ? (selectedGamePlatformId ?? platformId) : platformId;
            var normalizedPlatformId = PlatformInfoService.NormalizePlatformId(targetPlatformId);

            config.DefaultEmulators[normalizedPlatformId] = emulatorId;
            service.SaveConfig(config);
        }
    }
}