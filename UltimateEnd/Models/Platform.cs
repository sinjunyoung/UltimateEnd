using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using ReactiveUI;
using SkiaSharp;
using System;
using System.IO;
using UltimateEnd.Services;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Models
{
    public class Platform : ViewModelBase, IDisposable
    {
        private string _name = string.Empty;
        private string _imagePath = string.Empty;
        private Bitmap? _cachedImage;
        private Bitmap? _cachedLogo;

        public string Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string Id { get; set; } = string.Empty;

        public string FolderPath { get; set; } = string.Empty;

        public string MappedPlatformId { get; set; } = string.Empty;

        public string ImagePath
        {
            get => _imagePath;
            set
            {
                this.RaiseAndSetIfChanged(ref _imagePath, value);
                _cachedImage?.Dispose();
                _cachedImage = null;
                this.RaisePropertyChanged(nameof(ImageBitmap));
            }
        }

        private string _logoPath = string.Empty;

        public string LogoPath
        {
            get => _logoPath;
            set
            {
                this.RaiseAndSetIfChanged(ref _logoPath, value);
                _cachedLogo?.Dispose();
                _cachedLogo = null;
                this.RaisePropertyChanged(nameof(LogoImage));
            }
        }

        public Bitmap? LogoImage
        {
            get
            {
                if (_cachedLogo != null) return _cachedLogo;

                if (string.IsNullOrEmpty(LogoPath)) return null;

                try
                {
                    var converter = PathConverterFactory.Create?.Invoke();
                    var realPath = converter?.FriendlyPathToRealPath(LogoPath) ?? LogoPath;

                    if (Uri.TryCreate(realPath, UriKind.Absolute, out var uri))
                    {
                        bool isSvg = realPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

                        if (uri.Scheme == "avares")
                        {
                            if (isSvg)
                                _cachedLogo = LoadSvgAsBitmap(realPath, uri);
                            else
                            {
                                using var stream = AssetLoader.Open(uri);
                                _cachedLogo = new Bitmap(stream);
                            }
                            return _cachedLogo;
                        }
                        else if (uri.Scheme == "file")
                        {
                            if (isSvg)
                                _cachedLogo = LoadSvgAsBitmap(uri.LocalPath, uri);
                            else
                            {
                                using var stream = File.OpenRead(uri.LocalPath);
                                _cachedLogo = new Bitmap(stream);
                            }
                            return _cachedLogo;
                        }
                    }

                    return null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public Bitmap? ImageBitmap
        {
            get
            {
                if (_cachedImage != null) return _cachedImage;

                if (string.IsNullOrEmpty(ImagePath)) return null;

                try
                {
                    var converter = PathConverterFactory.Create?.Invoke();
                    var realPath = converter?.FriendlyPathToRealPath(ImagePath) ?? ImagePath;

                    if (realPath.StartsWith("avares://"))
                    {
                        var uri = new Uri(realPath);
                        bool isSvg = realPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);

                        if (isSvg)
                            _cachedImage = LoadSvgAsBitmap(realPath, uri);
                        else
                        {
                            using var stream = AssetLoader.Open(uri);
                            _cachedImage = new Bitmap(stream);
                        }

                        return _cachedImage;
                    }

                    _cachedImage = new Bitmap(realPath);

                    return _cachedImage;
                }
                catch
                {
                    return null;
                }
            }
        }

        private static Bitmap? LoadSvgAsBitmap(string path, Uri uri)
        {
            try
            {
                var svg = SvgSource.Load(path, uri);

                if (svg?.Picture == null) return null;

                var bounds = svg.Picture.CullRect;

                if (bounds.Width <= 0 || bounds.Height <= 0) return null;

                using var bitmap = new SKBitmap((int)bounds.Width, (int)bounds.Height);

                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    canvas.DrawPicture(svg.Picture);
                    canvas.Flush();
                }

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var memStream = new MemoryStream(data.ToArray());

                return new Bitmap(memStream);
            }
            catch
            {
                return null;
            }
        }

        public bool IsPlaylist { get; set; } = false;

        public void Dispose()
        {
            _cachedImage?.Dispose();
            _cachedImage = null;
            _cachedLogo?.Dispose();
            _cachedLogo = null;
        }
    }
}