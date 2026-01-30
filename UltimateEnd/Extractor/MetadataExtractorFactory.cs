using System;
using System.Collections.Generic;
using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.Extractor
{
    public static class MetadataExtractorFactory
    {
        private static readonly Dictionary<string, Func<IMetadataExtractor>> _extractors = new()
        {
            { "nintendoswitch", () =>
            {
                var factory = AppBaseFolderProviderFactory.Create.Invoke();
                string keyPath = Path.Combine(factory.GetSettingsFolder(), "prod.keys");

                return File.Exists(keyPath) ? new SwitchMetadataExtractor(keyPath): null;
            }},
            { "nintendods", () => new NdsMetadataExtractor() },
            { "3ds", () => new ThreeDSMetadataExtractor() },
        };

        public static bool IsSupported(string platformId) => _extractors.ContainsKey(platformId);

        public static IMetadataExtractor GetExtractor(string platformId)
        {
            if (!_extractors.TryGetValue(platformId, out var factory)) throw new NotSupportedException($"플랫폼 '{platformId}'은(는) ROM 메타데이터 추출을 지원하지 않습니다.");

            return factory();
        }

        public static string[] GetSupportedPlatforms() => [.. _extractors.Keys];
    }
}