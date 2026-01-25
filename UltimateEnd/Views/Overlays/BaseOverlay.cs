using Avalonia.Controls;
using Avalonia.Input;
using System;
using UltimateEnd.Enums;
using UltimateEnd.Managers;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public abstract class BaseOverlay : UserControl, IOverlay
    {
        public abstract bool Visible { get; }

        public abstract void Hide(HiddenState state);

        public abstract void Show();

        public event EventHandler Showing;
        public event EventHandler<HiddenEventArgs> Hidden;
        public event EventHandler Click;

        protected virtual void OnShowing(EventArgs e) => Showing?.Invoke(this, e);

        protected virtual void OnHidden(HiddenEventArgs e)
        {
            Hidden?.Invoke(this, e);
            ScreenSaverManager.Instance.OnAppResumed();
        }

        protected virtual void OnClick(EventArgs e) => Click?.Invoke(this, e);

        protected virtual void MovePrevious() { }

        protected virtual void MoveNext() { }

        protected virtual void SelectCurrent() { }

        protected async override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible)
            {
                base.OnKeyDown(e);
                return;
            }

            if (InputManager.IsButtonPressed(e, GamepadButton.ButtonB))
            {
                e.Handled = true;
                Hide(HiddenState.Cancel);
                return;
            }

            var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

            if (focusedElement is Slider slider)
            {
                if (InputManager.IsButtonPressed(e, GamepadButton.DPadUp))
                {
                    await WavSounds.Click();
                    e.Handled = true;
                    MovePrevious();
                    return;
                }

                if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
                {
                    await WavSounds.Click();
                    e.Handled = true;
                    MoveNext();
                    return;
                }

                if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
                {
                    await WavSounds.Click();
                    double step = (slider.Maximum - slider.Minimum) / 100.0;
                    slider.Value = Math.Max(slider.Minimum, slider.Value - step);
                    e.Handled = true;
                    return;
                }

                if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
                {
                    await WavSounds.Click();
                    double step = (slider.Maximum - slider.Minimum) / 100.0;
                    slider.Value = Math.Min(slider.Maximum, slider.Value + step);
                    e.Handled = true;
                    return;
                }

                if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA))
                {
                    await WavSounds.Click();
                    e.Handled = true;
                    MoveNext();
                    return;
                }

                e.Handled = true;
                return;
            }

            if (focusedElement is TextBox textBox)
            {
                if (InputManager.IsButtonPressed(e, GamepadButton.DPadUp))
                {
                    await WavSounds.Click();
                    e.Handled = true;
                    MovePrevious();
                    return;
                }

                if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
                {
                    await WavSounds.Click();
                    e.Handled = true;
                    MoveNext();
                    return;
                }

                if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA))
                {
                    await WavSounds.Click();
                    e.Handled = true;
                    MoveNext();
                    return;
                }

                base.OnKeyDown(e);
                return;
            }

            e.Handled = true;

            if (InputManager.IsButtonPressed(e, GamepadButton.DPadUp))
            {
                await WavSounds.Click();
                MovePrevious();
                return;
            }

            if (InputManager.IsButtonPressed(e, GamepadButton.DPadDown))
            {
                await WavSounds.Click();
                MoveNext();
                return;
            }

            if (InputManager.IsButtonPressed(e, GamepadButton.DPadLeft))
            {
                await WavSounds.Click();
                MovePrevious();
                return;
            }

            if (InputManager.IsButtonPressed(e, GamepadButton.DPadRight))
            {
                await WavSounds.Click();
                MoveNext();
                return;
            }

            if (InputManager.IsAnyButtonPressed(e, GamepadButton.ButtonA))
            {
                SelectCurrent();
                return;
            }

            base.OnKeyDown(e);
        }
    }

    public sealed class HiddenEventArgs : EventArgs
    {
        public static readonly new HiddenEventArgs Empty = new() { State = HiddenState.Close };

        public HiddenState State { get; set; }
    }
}