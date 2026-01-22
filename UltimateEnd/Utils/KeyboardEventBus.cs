using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace UltimateEnd.Utils
{
    public static class KeyboardEventBus
    {
        private static readonly Subject<bool> _keyboardVisibilitySubject = new();

        public static IObservable<bool> KeyboardVisibility => _keyboardVisibilitySubject.AsObservable();

        public static void NotifyKeyboardVisibility(bool isVisible) => _keyboardVisibilitySubject.OnNext(isVisible);
    }
}