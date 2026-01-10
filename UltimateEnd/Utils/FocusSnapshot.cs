using Avalonia.Input;
using Avalonia.Threading;

namespace UltimateEnd.Utils
{
    public class FocusSnapshot(IInputElement? focus)
    {
        private readonly IInputElement? _savedFocus = focus;

        public void Restore() => Restore(DispatcherPriority.Input);

        public void Restore(DispatcherPriority priority) => FocusHelper.SetFocus(_savedFocus, priority);

        public bool HasFocus => _savedFocus != null;

        public IInputElement? SavedElement => _savedFocus;
    }
}