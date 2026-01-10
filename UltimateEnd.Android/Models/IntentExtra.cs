using ReactiveUI;

namespace UltimateEnd.Android.Models
{
    public class IntentExtra : ReactiveObject
    {
        private string _type = "string";
        private string _key = string.Empty;
        private string _value = string.Empty;

        public string Type
        {
            get => _type;
            set => this.RaiseAndSetIfChanged(ref _type, value);
        }

        public string Key
        {
            get => _key;
            set => this.RaiseAndSetIfChanged(ref _key, value);
        }

        public string Value
        {
            get => _value;
            set => this.RaiseAndSetIfChanged(ref _value, value);
        }
    }
}