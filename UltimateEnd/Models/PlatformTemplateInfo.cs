using Avalonia.Media.Imaging;
using System;

namespace UltimateEnd.Models
{
    public class PlatformTemplateInfo : IDisposable
    {
        public string Name { get; set; }

        public string Id { get; set; }

        public Bitmap? Image { get; set; }

        public bool IsSelected { get; set; }

        public void Dispose()
        {
            Image?.Dispose();
        }
    }
}