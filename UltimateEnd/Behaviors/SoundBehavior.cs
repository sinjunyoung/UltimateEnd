using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Helpers;

namespace UltimateEnd.Behaviors
{    public static class SoundBehavior
    {
        public static readonly AttachedProperty<SoundType?> ClickSoundProperty = AvaloniaProperty.RegisterAttached<Button, SoundType?>("ClickSound", typeof(SoundBehavior));

        public static SoundType? GetClickSound(Button element) => element.GetValue(ClickSoundProperty);

        public static void SetClickSound(Button element, SoundType? value) => element.SetValue(ClickSoundProperty, value);

        static SoundBehavior() => ClickSoundProperty.Changed.AddClassHandler<Button>(OnClickSoundChanged);

        private static void OnClickSoundChanged(Button button, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is SoundType soundType)
            {
                button.Click -= OnButtonClick;
                button.Click += OnButtonClick;
            }
            else
                button.Click -= OnButtonClick;
        }

        private static async void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                var soundType = GetClickSound(button);
                await PlaySound(soundType);
            }
        }

        private static async Task PlaySound(SoundType? soundType)
        {
            if (soundType == null) return;

            try
            {
                switch (soundType)
                {
                    case SoundType.Click:
                        await WavSounds.Click();
                        break;
                    case SoundType.OK:
                        await WavSounds.OK();
                        break;
                    case SoundType.Cancel:
                        await WavSounds.Cancel();
                        break;
                }
            }
            catch { }
        }
    }
}