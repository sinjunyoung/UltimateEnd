using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;

namespace UltimateEnd.Behaviors
{
    public static class FocusOnTrue
    {
        public static readonly AttachedProperty<bool> FocusOnTrueProperty = AvaloniaProperty.RegisterAttached<object, Control, bool>("FocusOnTrue", false, false);

        static FocusOnTrue()
        {
            FocusOnTrueProperty.OverrideMetadata<TextBox>(
                new StyledPropertyMetadata<bool>(
                    false,
                    BindingMode.OneWay
                )
            );

            FocusOnTrueProperty.Changed.AddClassHandler<TextBox>((sender, args) =>
            {
                if (args.NewValue is true)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        sender.Focus();
                        sender.SelectAll();
                    }, DispatcherPriority.Input);
                }
            });
        }

        public static void SetFocusOnTrue(Control element, bool value) => element.SetValue(FocusOnTrueProperty, value);

        public static bool GetFocusOnTrue(Control element) => element.GetValue(FocusOnTrueProperty);
    }
}