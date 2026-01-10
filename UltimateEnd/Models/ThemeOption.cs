using ReactiveUI;
using UltimateEnd.Enums;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Models
{
    public class ThemeOption : ViewModelBase
    {
        private bool _isSelected;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public AppTheme Theme { get; set; }

        public string PreviewColor1 { get; set; } = string.Empty;

        public string PreviewColor2 { get; set; } = string.Empty;

        public string PreviewColor3 { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
    }
}