using ReactiveUI;

namespace UltimateEnd.Models
{
    public class GameGenreItem : ReactiveObject
    {
        private int _id;
        private string _genre = string.Empty;
        private bool _isSelected;

        public int Id
        {
            get => _id;
            set => this.RaiseAndSetIfChanged(ref _id, value);
        }

        public string Genre
        {
            get => _genre;
            set => this.RaiseAndSetIfChanged(ref _genre, value);
        }

        public string DisplayText => string.IsNullOrWhiteSpace(Genre) ? "장르 없음" : Genre;

        public string DisplayIdText => $"[{Id}]";

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
    }
}