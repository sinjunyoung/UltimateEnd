using ReactiveUI;

namespace UltimateEnd.Models
{
    public class FolderItem : ReactiveObject
    {
        public enum ItemType { Folder, Game }

        private bool _isSelected;

        public ItemType Type { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? SubFolder { get; set; }

        public GameMetadata? Game { get; set; }

        public int GameCount { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }

        public bool IsFolder => Type == ItemType.Folder;
        public bool IsGame => Type == ItemType.Game;

        public static FolderItem CreateFolder(string folderName, int gameCount)
        {
            return new FolderItem
            {
                Type = ItemType.Folder,
                Name = folderName,
                SubFolder = folderName,
                GameCount = gameCount
            };
        }

        public static FolderItem CreateGame(GameMetadata game)
        {
            return new FolderItem
            {
                Type = ItemType.Game,
                Name = game.DisplayTitle,
                Game = game
            };
        }
    }
}