using ReactiveUI;

namespace UltimateEnd.Android.Models
{
    public class PlatformItemViewModel : ReactiveObject
    {
        private bool _isSelected;

        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public object? Image { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
    }
}