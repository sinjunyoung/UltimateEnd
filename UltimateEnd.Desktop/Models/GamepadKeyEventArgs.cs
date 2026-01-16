using Avalonia.Input;
using UltimateEnd.Enums;

namespace UltimateEnd.Desktop.Models
{
    public class GamepadKeyEventArgs : KeyEventArgs
    {
        public bool IsFromGamepad { get; set; }

        public GamepadButton? OriginalButton { get; set; }
    }
}