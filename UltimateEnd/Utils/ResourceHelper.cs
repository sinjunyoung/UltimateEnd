using Avalonia.Platform;
using System;

namespace UltimateEnd.Utils
{
    public class ResourceHelper
    {
        public static string? GetPlatformImage(string platformid)
        {
            string[] formats = ["png", "jpg", "svg"];

            foreach (var format in formats)
            {
                var uri = new Uri($"avares://UltimateEnd/Assets/Platforms/{platformid}.{format}");

                if (AssetLoader.Exists(uri)) return uri.ToString();
            }

            return null;

        }

        public static string? GetLogoImage(string platformid)
        {
            string[] formats = ["png", "jpg", "svg"];

            foreach (var format in formats)
            {
                var uri = new Uri($"avares://UltimateEnd/Assets/Logos/{platformid}.{format}");

                if (AssetLoader.Exists(uri)) return uri.ToString();
            }

            return null;
        }

        public static string? GetIconImage(string iconid)
        {
            string[] formats = ["png", "jpg", "svg"];

            foreach (var format in formats)
            {
                var uri = new Uri($"avares://UltimateEnd/Assets/Icons/{iconid}.{format}");

                if (AssetLoader.Exists(uri)) return uri.ToString();
            }

            return null;
        }

        public static string? GetImage(string iconid)
        {
            string[] formats = ["png", "jpg", "svg"];

            foreach (var format in formats)
            {
                var uri = new Uri($"avares://UltimateEnd/Assets/Images/{iconid}.{format}");

                if (AssetLoader.Exists(uri)) return uri.ToString();
            }

            return null;
        }
    }
}