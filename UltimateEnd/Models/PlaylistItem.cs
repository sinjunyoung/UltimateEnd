using ReactiveUI;

namespace UltimateEnd.Models
{
    public class PlaylistItem : ReactiveObject
    {
        private bool _isSelected;

        public Playlist Playlist { get; set; } = null!;

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
    }
}