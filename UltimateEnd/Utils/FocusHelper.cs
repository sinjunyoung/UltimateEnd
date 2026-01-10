using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;

namespace UltimateEnd.Utils
{
    public static class FocusHelper
    {
        public static IInputElement? GetCurrentFocus()
        {
            var lifetime = Application.Current?.ApplicationLifetime;

            if (lifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;

                return TopLevel.GetTopLevel(mainWindow)?.FocusManager?.GetFocusedElement();
            }

            if (lifetime is ISingleViewApplicationLifetime single)
                return TopLevel.GetTopLevel(single.MainView)?.FocusManager?.GetFocusedElement();

            return null;
        }

        public static IInputElement? GetCurrentFocus(Visual control) => TopLevel.GetTopLevel(control)?.FocusManager?.GetFocusedElement();

        public static void SetFocus(IInputElement? element) => SetFocus(element, DispatcherPriority.Input);

        public static void SetFocus(IInputElement? element, DispatcherPriority priority)
        {
            if (element != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    element.Focus();
                }, priority);
            }
        }

        public static bool SetFocusImmediate(IInputElement? element) => element?.Focus() ?? false;

        public static FocusSnapshot CreateSnapshot() => new(GetCurrentFocus());

        public static FocusSnapshot CreateSnapshot(Visual control) => new(GetCurrentFocus(control));
    }
}