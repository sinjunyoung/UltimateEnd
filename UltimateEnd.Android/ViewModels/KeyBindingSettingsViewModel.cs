using System.Collections.Generic;
using UltimateEnd.Android.Models;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Android.ViewModels
{
    public class KeyBindingSettingsViewModel : KeyBindingSettingsViewModelBase
    {
        public List<KeyBindingItem> ButtonItems { get; }

        public KeyBindingSettingsViewModel() : base()
        {
            ButtonItems =
            [
                new("DPadUp", "↑", "D-Pad Up", () => GetAndroidKeyDisplayName(DPadUp), v => DPadUp = v),
                new("DPadDown", "↓", "D-Pad Down", () => GetAndroidKeyDisplayName(DPadDown), v => DPadDown = v),
                new("DPadLeft", "←", "D-Pad Left", () => GetAndroidKeyDisplayName(DPadLeft), v => DPadLeft = v),
                new("DPadRight", "→", "D-Pad Right", () => GetAndroidKeyDisplayName(DPadRight), v => DPadRight = v),
                new("ButtonA", "A", "A 버튼", () => GetAndroidKeyDisplayName(ButtonA), v => ButtonA = v),
                new("ButtonB", "B", "B 버튼", () => GetAndroidKeyDisplayName(ButtonB), v => ButtonB = v),
                new("ButtonX", "X", "X 버튼", () => GetAndroidKeyDisplayName(ButtonX), v => ButtonX = v),
                new("ButtonY", "Y", "Y 버튼", () => GetAndroidKeyDisplayName(ButtonY), v => ButtonY = v),
                new("LeftBumper", "LB", "Left Bumper", () => GetAndroidKeyDisplayName(LeftBumper), v => LeftBumper = v),
                new("RightBumper", "RB", "Right Bumper", () => GetAndroidKeyDisplayName(RightBumper), v => RightBumper = v),
                new("Start", "▶", "Start", () => GetAndroidKeyDisplayName(Start), v => Start = v),
                new("Select", "◀", "Select", () => GetAndroidKeyDisplayName(Select), v => Select = v),
    ];

            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DPadUp) ||
                    e.PropertyName == nameof(DPadDown) ||
                    e.PropertyName == nameof(DPadLeft) ||
                    e.PropertyName == nameof(DPadRight) ||
                    e.PropertyName == nameof(ButtonA) ||
                    e.PropertyName == nameof(ButtonB) ||
                    e.PropertyName == nameof(ButtonX) ||
                    e.PropertyName == nameof(ButtonY) ||
                    e.PropertyName == nameof(LeftBumper) ||
                    e.PropertyName == nameof(RightBumper) ||
                    e.PropertyName == nameof(Start) ||
                    e.PropertyName == nameof(Select))
                {
                    NotifyButtonItemsChanged();
                }
            };
        }

        private void NotifyButtonItemsChanged()
        {
            foreach (var item in ButtonItems)
                item.NotifyCurrentValueChanged();
        }

        protected override string GetButtonDisplayName(string buttonName)
        {
            return buttonName switch
            {
                "DPadUp" => "↑ Up",
                "DPadDown" => "↓ Down",
                "DPadLeft" => "← Left",
                "DPadRight" => "→ Right",
                "ButtonA" => "A Button",
                "ButtonB" => "B Button",
                "ButtonX" => "X Button",
                "ButtonY" => "Y Button",
                "LeftBumper" => "LB",
                "RightBumper" => "RB",
                "Start" => "Start",
                "Select" => "Select",
                _ => buttonName
            };
        }

        private static string GetAndroidKeyDisplayName(string keyName)
        {
            return keyName switch
            {
                "Return" => "Button A",
                "Escape" => "Button B",
                "X" => "Button X",
                "F" => "Button Y",
                "PageUp" => "L1",
                "PageDown" => "R1",
                "Up" => "D-Pad Up",
                "Down" => "D-Pad Down",
                "Left" => "D-Pad Left",
                "Right" => "D-Pad Right",
                "Space" => "Start",
                "F1" => "Select",
                _ => keyName
            };
        }

        protected override void ResetToDefault()
        {
            DPadUp = "Up";
            DPadDown = "Down";
            DPadLeft = "Left";
            DPadRight = "Right";
            ButtonA = "Return";
            ButtonB = "Escape";
            ButtonX = "X";
            ButtonY = "F";
            LeftBumper = "PageUp";
            RightBumper = "PageDown";
            Start = "Space";
            Select = "F1";
        }
    }
}