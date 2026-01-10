using Avalonia.Media.Imaging;
using ReactiveUI;

namespace UltimateEnd.Android.Models
{
    public class AppItemViewModel : ReactiveObject
    {
        private bool _isSelected;

        public string DisplayName { get; set; } = string.Empty;

        public string PackageName { get; set; } = string.Empty;

        public string ActivityName { get; set; } = string.Empty;

        public Bitmap? Icon { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
    }
}