using ReactiveUI;
using System;

namespace UltimateEnd.ViewModels
{
    public class BackupItemViewModel : ReactiveObject
    {
        private bool _isSelected;

        public string FileId { get; set; }

        public string FileName { get; set; }

        public string DisplayName { get; set; }

        public DateTime ModifiedTime { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }
    }
}