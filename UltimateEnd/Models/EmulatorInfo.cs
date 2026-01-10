using Avalonia.Media.Imaging;
using ReactiveUI;
using System;
namespace UltimateEnd.Models
{
    public class EmulatorInfo: ReactiveObject, IDisposable
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public Bitmap Icon { get; set; } = null;

        public bool IsDefault { get; set; }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }

        public void Dispose()
        {
            Icon?.Dispose();
            Icon = null;
        }
    }
}