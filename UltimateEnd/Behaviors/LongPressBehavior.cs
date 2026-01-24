using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;

namespace UltimateEnd.Behaviors
{
    public static class LongPressBehavior
    {
        private class LongPressState
        {
            public DispatcherTimer? Timer { get; set; }

            public object? CurrentItem { get; set; }

            public Point? PressPoint { get; set; }

            public bool WasLongPressed { get; set; }

            public PointerPressedEventArgs? PressedEventArgs { get; set; }
        }

        private const double MovementThresholdSquared = 25.0;

        public static readonly AttachedProperty<int> DurationProperty = AvaloniaProperty.RegisterAttached<Control, int>("Duration", typeof(LongPressBehavior), 300);
        public static readonly AttachedProperty<string?> MethodNameProperty = AvaloniaProperty.RegisterAttached<Control, string?>("MethodName", typeof(LongPressBehavior));
        public static readonly AttachedProperty<object?> TargetProperty = AvaloniaProperty.RegisterAttached<Control, object?>("Target", typeof(LongPressBehavior));
        public static readonly AttachedProperty<bool> IsEnabledProperty = AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(LongPressBehavior), false);

        static LongPressBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>((control, args) =>
            {
                if (args.NewValue is bool isEnabled)
                    OnIsEnabledChanged(control, isEnabled);
            });
        }

        private static readonly AttachedProperty<LongPressState?> StateProperty = AvaloniaProperty.RegisterAttached<Control, LongPressState?>("State", typeof(LongPressBehavior));

        public static void SetDuration(Control element, int value) => element.SetValue(DurationProperty, value);

        public static int GetDuration(Control element) => element.GetValue(DurationProperty);

        public static void SetMethodName(Control element, string? value) => element.SetValue(MethodNameProperty, value);

        public static string? GetMethodName(Control element) => element.GetValue(MethodNameProperty);

        public static void SetTarget(Control element, object? value) => element.SetValue(TargetProperty, value);

        public static object? GetTarget(Control element) => element.GetValue(TargetProperty);

        public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);

        private static void SetState(Control element, LongPressState? value) => element.SetValue(StateProperty, value);

        private static LongPressState? GetState(Control element) => element.GetValue(StateProperty);

        public static bool WasLongPressed(Control element)
        {
            var state = GetState(element);
            
            if (state == null) return false;

            bool result = state.WasLongPressed;

            if (result) state.WasLongPressed = false;

            return result;
        }

        private static void OnIsEnabledChanged(Control element, bool isEnabled)
        {
            if (isEnabled)
            {
                SetState(element, new LongPressState());

                element.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
                element.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
                element.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel);
                element.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);

                element.AddHandler(Gestures.TappedEvent, OnTapped, RoutingStrategies.Tunnel);

                element.LostFocus += (s, e) => CancelLongPress(s as Control);

                if (element.GetVisualRoot() is Window window)
                    window.Deactivated += (s, e) => CancelLongPress(element);
            }
            else
            {
                element.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
                element.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
                element.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
                element.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
                element.RemoveHandler(Gestures.TappedEvent, OnTapped);

                var state = GetState(element);
                state?.Timer?.Stop();
                SetState(element, null);
            }
        }

        private static void OnTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control element) return;

            var state = GetState(element);

            if (state?.WasLongPressed == true) e.Handled = true;
        }

        private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control element) return;

            var state = GetState(element);

            if (state == null) return;

            if (e.ClickCount >= 2)
            {
                CancelLongPress(element);
                return;
            }

            if (state.Timer != null)
            {
                state.Timer.Stop();
                state.Timer.Tick -= TimerTick;
                state.Timer = null;
            }

            state.WasLongPressed = false;
            state.PressPoint = e.GetCurrentPoint(element).Position;
            state.PressedEventArgs = e;

            var dataContext = (element as IDataContextProvider)?.DataContext;

            if (dataContext == null)
            {
                state.PressPoint = null;
                return;
            }

            state.CurrentItem = dataContext;

            state.Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(GetDuration(element)),
                Tag = element
            };
            state.Timer.Tick += TimerTick;
            state.Timer.Start();
        }

        private static void TimerTick(object? sender, EventArgs e)
        {
            if (sender is not DispatcherTimer timer) return;

            timer.Stop();
            timer.Tick -= TimerTick;

            if (timer.Tag is not Control element) return;

            timer.Tag = null;

            var state = GetState(element);

            if (state?.CurrentItem == null) return;

            state.WasLongPressed = true;

            if (state.PressedEventArgs != null) state.PressedEventArgs.Handled = true;

            var methodName = GetMethodName(element);
            var target = GetTarget(element);

            if (!string.IsNullOrEmpty(methodName) && target != null)
            {
                try
                {
                    var method = target.GetType().GetMethod(
                        methodName,
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance
                    );
                    var dataContext = (element as IDataContextProvider)?.DataContext;
                    method?.Invoke(target, [element, dataContext]);
                }
                catch { }
            }

            state.CurrentItem = null;
            state.PressedEventArgs = null;
        }

        private static void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Control element) return;

            var state = GetState(element);

            if (state?.Timer?.IsEnabled != true) return;

            if (state.PressPoint == null) return;

            var currentPoint = e.GetCurrentPoint(element).Position;
            var pressPoint = state.PressPoint.Value;

            var dx = currentPoint.X - pressPoint.X;
            var dy = currentPoint.Y - pressPoint.Y;

            if ((dx * dx + dy * dy) > MovementThresholdSquared) CancelLongPress(element);
        }

        private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => CancelLongPress(sender as Control);

        private static void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => CancelLongPress(sender as Control);

        private static void CancelLongPress(Control? element)
        {
            if (element == null) return;

            var state = GetState(element);
            if (state?.Timer == null) return;

            bool wasRunning = state.Timer.IsEnabled;

            state.Timer.Stop();
            state.Timer.Tag = null;
            state.CurrentItem = null;
            state.PressedEventArgs = null;

            if (wasRunning) state.WasLongPressed = false;
        }
    }
}