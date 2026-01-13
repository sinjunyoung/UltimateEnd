using Avalonia.Input;
using System.Threading.Tasks;
using UltimateEnd.Enums;

namespace UltimateEnd.Utils
{
    public static class KeySoundHelper
    {
        public static async Task PlaySoundForKey(Key key)
        {
            if (InputManager.IsAnyButtonPressed(key, GamepadButton.ButtonB, GamepadButton.Select))
                await WavSounds.Cancel();
            else if (InputManager.IsAnyButtonPressed(key,
                GamepadButton.DPadUp,
                GamepadButton.DPadDown,
                GamepadButton.DPadLeft,
                GamepadButton.DPadRight,
                GamepadButton.LeftBumper,
                GamepadButton.RightBumper))
            {
                await WavSounds.Click();
            }
            else if (key == Key.Back)
                await WavSounds.Cancel();
        }

        public static async Task PlaySoundForKeyEvent(KeyEventArgs e) => await PlaySoundForKey(e.Key);
    }
}