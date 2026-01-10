using Avalonia.Media.Imaging;
using System;

namespace UltimateEnd.Models
{
    internal class GameMetadataCache
    {
        public string? CoverPath { get; set; }
        public string? LogoPath { get; set; }
        public string? VideoPath { get; set; }
        public bool? HasVideo { get; set; }
        public string? RomFullPath { get; set; }
        public string? SearchableText { get; set; }

        public GamePlayHistory? PlayHistory { get; set; }

        public bool PlayHistoryValid { get; set; }

        public WeakReference<Bitmap>? CoverBitmapRef { get; set; }

        public string? LastCoverPath { get; set; }

        public WeakReference<Bitmap>? LogoBitmapRef { get; set; }

        public string? LastLogoPath { get; set; }

        public void InvalidateAll()
        {
            CoverPath = null;
            LogoPath = null;
            VideoPath = null;
            HasVideo = null;
            RomFullPath = null;
            PlayHistoryValid = false;
            PlayHistory = null;
        }

        public void InvalidateMedia()
        {
            CoverPath = null;
            LogoPath = null;
            VideoPath = null;
            HasVideo = null;
            CoverBitmapRef = null;
            LastCoverPath = null;
            LogoBitmapRef = null;
            LastLogoPath = null;
        }
    }
}