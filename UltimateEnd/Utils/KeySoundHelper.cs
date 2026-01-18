using Avalonia.Input;
using System.Threading.Tasks;
using UltimateEnd.Enums;

namespace UltimateEnd.Utils
{
    public static class KeySoundHelper
    {
        public static async Task PlaySoundForKey(KeyEventArgs e)
        {
            if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonB, GamepadButton.Select))
                await WavSounds.Cancel();
            else if (InputManager.IsAnyButtonPressed(e,
                GamepadButton.DPadUp,
                GamepadButton.DPadDown,
                GamepadButton.DPadLeft,
                GamepadButton.DPadRight,
                GamepadButton.LeftBumper,
                GamepadButton.RightBumper))
            {
                await WavSounds.Click();
            }
            else if (e.Key == Key.Back)
                await WavSounds.Cancel();
        }

        public static async Task PlaySoundForKeyEvent(KeyEventArgs e) => await PlaySoundForKey(e);
    }
}