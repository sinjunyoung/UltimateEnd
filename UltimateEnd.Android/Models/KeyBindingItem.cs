using ReactiveUI;
using System;

namespace UltimateEnd.Android.Models
{
    public class KeyBindingItem(string buttonName, string icon, string description, Func<string> getValue, Action<string> setValue) : ReactiveObject
    {
        private string _currentValue = getValue();

        public string ButtonName { get; } = buttonName;

        public string Icon { get; } = icon;

        public string Description { get; } = description;

        public string CurrentValue
        {
            get => _currentValue;
            set => this.RaiseAndSetIfChanged(ref _currentValue, value);
        }

        private Func<string> _getValue = getValue;
        private Action<string> _setValue = setValue;

        public void NotifyCurrentValueChanged() => CurrentValue = _getValue();
    }
}