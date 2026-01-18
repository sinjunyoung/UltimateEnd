using Avalonia.Input;
using Avalonia.Interactivity;
using UltimateEnd.Enums;

namespace UltimateEnd.Models
{
    public class GamepadKeyEventArgs : KeyEventArgs
    {
        public bool IsFromGamepad { get; set; }

        public GamepadButton? OriginalButton { get; set; }

        public int PhysicalButtonIndex { get; set; } = -1;

        public GamepadKeyEventArgs()
        {
        }

        public GamepadKeyEventArgs(RoutedEvent routedEvent, Key key, GamepadButton originalButton, int physicalButtonIndex = -1)
        {
            RoutedEvent = routedEvent;
            Key = key;
            IsFromGamepad = true;
            OriginalButton = originalButton;
            PhysicalButtonIndex = physicalButtonIndex;
        }
    }
}