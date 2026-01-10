using ReactiveUI;

namespace UltimateEnd.Models
{
    public class RomsBasePathItem : ReactiveObject
    {
        private string _path = string.Empty;

        public string Path
        {
            get => _path;
            set => this.RaiseAndSetIfChanged(ref _path, value);
        }
    }
}