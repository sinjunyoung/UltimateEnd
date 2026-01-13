using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
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

            if (e.Source is TextBox)
            {
                base.OnKeyDown(e);
                return;
            }

            e.Handled = true;

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                e.Handled = true;
                Hide(HiddenState.Cancel);
                return;
            }
            if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadUp))
            {
                await WavSounds.Click();
                e.Handled = true;
                MovePrevious();
                return;
            }
            if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadDown))
            {
                await WavSounds.Click();
                e.Handled = true;
                MoveNext();
                return;
            }
            if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;
                SelectCurrent();
                return;
            }

            base.OnKeyDown(e);
        }
    }

    public sealed class HiddenEventArgs: EventArgs
    {
        public static readonly new HiddenEventArgs Empty = new() { State = HiddenState.Close };

        public HiddenState State { get; set; }
    }
}