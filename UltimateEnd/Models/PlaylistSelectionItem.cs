using ReactiveUI;

namespace UltimateEnd.Models
{
    public class PlaylistSelectionItem : ReactiveObject
    {
        private bool _isAdded;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        public bool IsAdded
        {
            get => _isAdded;
            set => this.RaiseAndSetIfChanged(ref _isAdded, value);
        }
    }
}