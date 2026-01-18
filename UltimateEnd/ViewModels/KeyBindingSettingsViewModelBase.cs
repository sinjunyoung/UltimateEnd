using Avalonia.Input;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.ViewModels
{
    public abstract class KeyBindingSettingsViewModelBase : ViewModelBase
    {
        #region Fields

        private bool _isBinding;
        private string _bindingButtonName = string.Empty;
        protected string _currentBindingKey = string.Empty;

        private string _dPadUp = "Up";
        private string _dPadDown = "Down";
        private string _dPadLeft = "Left";
        private string _dPadRight = "Right";
        private string _buttonA = "Return";
        private string _buttonB = "Escape";
        private string _buttonX = "X";
        private string _buttonY = "F";
        private string _leftBumper = "PageUp";
        private string _rightBumper = "PageDown";
        private string _leftTrigger = "LeftCtrl";
        private string _rightTrigger = "LeftAlt";
        private string _start = "Return";
        private string _select = "Escape";

        #endregion

        #region Properties - 키 바인딩

        public string DPadUp
        {
            get => _dPadUp;
            set => this.RaiseAndSetIfChanged(ref _dPadUp, value);
        }

        public string DPadDown
        {
            get => _dPadDown;
            set => this.RaiseAndSetIfChanged(ref _dPadDown, value);
        }

        public string DPadLeft
        {
            get => _dPadLeft;
            set => this.RaiseAndSetIfChanged(ref _dPadLeft, value);
        }

        public string DPadRight
        {
            get => _dPadRight;
            set => this.RaiseAndSetIfChanged(ref _dPadRight, value);
        }

        public string ButtonA
        {
            get => _buttonA;
            set => this.RaiseAndSetIfChanged(ref _buttonA, value);
        }

        public string ButtonB
        {
            get => _buttonB;
            set => this.RaiseAndSetIfChanged(ref _buttonB, value);
        }

        public string ButtonX
        {
            get => _buttonX;
            set => this.RaiseAndSetIfChanged(ref _buttonX, value);
        }

        public string ButtonY
        {
            get => _buttonY;
            set => this.RaiseAndSetIfChanged(ref _buttonY, value);
        }

        public string LeftBumper
        {
            get => _leftBumper;
            set => this.RaiseAndSetIfChanged(ref _leftBumper, value);
        }

        public string RightBumper
        {
            get => _rightBumper;
            set => this.RaiseAndSetIfChanged(ref _rightBumper, value);
        }

        public string LeftTrigger
        {
            get => _leftTrigger;
            set => this.RaiseAndSetIfChanged(ref _leftTrigger, value);
        }

        public string RightTrigger
        {
            get => _rightTrigger;
            set => this.RaiseAndSetIfChanged(ref _rightTrigger, value);
        }

        public string Start
        {
            get => _start;
            set => this.RaiseAndSetIfChanged(ref _start, value);
        }

        public string Select
        {
            get => _select;
            set => this.RaiseAndSetIfChanged(ref _select, value);
        }

        #endregion

        #region Properties - 바인딩 상태

        public bool IsBinding
        {
            get => _isBinding;
            set => this.RaiseAndSetIfChanged(ref _isBinding, value);
        }

        public string BindingButtonName
        {
            get => _bindingButtonName;
            set => this.RaiseAndSetIfChanged(ref _bindingButtonName, value);
        }

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> ResetToDefaultCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> GoBackCommand { get; }

        #endregion

        #region Events

        public event Action? BackRequested;

        #endregion

        #region Constructor

        protected KeyBindingSettingsViewModelBase()
        {
            LoadSettings();

            ResetToDefaultCommand = ReactiveCommand.CreateFromTask(ResetToDefault);
            SaveCommand = ReactiveCommand.CreateFromTask(SaveAndGoBack);
            GoBackCommand = ReactiveCommand.CreateFromTask(GoBackAsync);
        }

        #endregion

        #region Public Methods

        public void StartBinding(string buttonName)
        {
            _currentBindingKey = buttonName;
            BindingButtonName = GetButtonDisplayName(buttonName);
            IsBinding = true;
        }

        public async void HandleKeyPress(Key key)
        {
            if (!IsBinding || string.IsNullOrEmpty(_currentBindingKey)) return;

            if (key == Key.None) return;

            string keyName = NormalizeKeyName(key);

            AssignKeyToButton(_currentBindingKey, keyName);

            await WavSounds.OK();

            IsBinding = false;
            _currentBindingKey = string.Empty;
        }

        public void GoBack() => BackRequested?.Invoke();

        #endregion

        #region Protected Virtual Methods - 플랫폼별 확장 포인트

        protected virtual void LoadPlatformSpecificSettings(Models.AppSettings settings)
        {
        }

        protected virtual void SavePlatformSpecificSettings(Models.AppSettings settings)
        {
        }

        protected virtual void ResetPlatformSpecificDefaults()
        {
        }

        protected virtual async Task ShowSaveCompletionMessage()
        {
            await Task.CompletedTask;
        }

        protected virtual string GetButtonDisplayName(string buttonName)
        {
            return buttonName switch
            {
                "DPadUp" => "↑ (D-Pad Up)",
                "DPadDown" => "↓ (D-Pad Down)",
                "DPadLeft" => "← (D-Pad Left)",
                "DPadRight" => "→ (D-Pad Right)",
                "ButtonA" => "A 버튼",
                "ButtonB" => "B 버튼",
                "ButtonX" => "X 버튼",
                "ButtonY" => "Y 버튼",
                "LeftBumper" => "LB (Left Bumper)",
                "RightBumper" => "RB (Right Bumper)",
                "LeftTrigger" => "LT (Left Trigger)",
                "RightTrigger" => "RT (Right Trigger)",
                "Start" => "Start",
                "Select" => "Select",
                _ => buttonName
            };
        }

        #endregion

        #region Private Methods - 설정 관리

        private void LoadSettings()
        {
            var settings = SettingsService.LoadSettings();

            if (settings.KeyBindings != null && settings.KeyBindings.Count > 0)
            {
                DPadUp = settings.KeyBindings.GetValueOrDefault("DPadUp", "Up");
                DPadDown = settings.KeyBindings.GetValueOrDefault("DPadDown", "Down");
                DPadLeft = settings.KeyBindings.GetValueOrDefault("DPadLeft", "Left");
                DPadRight = settings.KeyBindings.GetValueOrDefault("DPadRight", "Right");
                ButtonA = settings.KeyBindings.GetValueOrDefault("ButtonA", "Return");
                ButtonB = settings.KeyBindings.GetValueOrDefault("ButtonB", "Escape");
                ButtonX = settings.KeyBindings.GetValueOrDefault("ButtonX", "X");
                ButtonY = settings.KeyBindings.GetValueOrDefault("ButtonY", "F");
                LeftBumper = settings.KeyBindings.GetValueOrDefault("LeftBumper", "PageUp");
                RightBumper = settings.KeyBindings.GetValueOrDefault("RightBumper", "PageDown");
                LeftTrigger = settings.KeyBindings.GetValueOrDefault("LeftTrigger", "LeftCtrl");
                RightTrigger = settings.KeyBindings.GetValueOrDefault("RightTrigger", "LeftAlt");
                Start = settings.KeyBindings.GetValueOrDefault("Start", "Return");
                Select = settings.KeyBindings.GetValueOrDefault("Select", "Escape");
            }

            LoadPlatformSpecificSettings(settings);
        }

        protected virtual async Task ResetToDefault()
        {
            var snap = FocusHelper.CreateSnapshot();

            await WavSounds.OK();

            DPadUp = "Up";
            DPadDown = "Down";
            DPadLeft = "Left";
            DPadRight = "Right";
            ButtonA = "Return";
            ButtonB = "Escape";
            ButtonX = "X";
            ButtonY = "F";
            LeftBumper = "PageUp";
            RightBumper = "PageDown";
            LeftTrigger = "LeftCtrl";
            RightTrigger = "LeftAlt";
            Start = "Return";
            Select = "Escape";

            ResetPlatformSpecificDefaults();

            snap.Restore();
        }

        private async Task SaveAndGoBack()
        {
            var snap = FocusHelper.CreateSnapshot();

            if (HasDuplicateKeys())
            {
                await WavSounds.Cancel();
                await DialogService.Instance.ShowWarning("중복된 키가 있습니다.\n다른 키를 할당해주세요.");

                snap.Restore();

                return;
            }

            var settings = SettingsService.LoadSettings();

            settings.KeyBindings ??= [];

            settings.KeyBindings["DPadUp"] = DPadUp;
            settings.KeyBindings["DPadDown"] = DPadDown;
            settings.KeyBindings["DPadLeft"] = DPadLeft;
            settings.KeyBindings["DPadRight"] = DPadRight;
            settings.KeyBindings["ButtonA"] = ButtonA;
            settings.KeyBindings["ButtonB"] = ButtonB;
            settings.KeyBindings["ButtonX"] = ButtonX;
            settings.KeyBindings["ButtonY"] = ButtonY;
            settings.KeyBindings["LeftBumper"] = LeftBumper;
            settings.KeyBindings["RightBumper"] = RightBumper;
            settings.KeyBindings["LeftTrigger"] = LeftTrigger;
            settings.KeyBindings["RightTrigger"] = RightTrigger;
            settings.KeyBindings["Start"] = Start;
            settings.KeyBindings["Select"] = Select;

            SavePlatformSpecificSettings(settings);

            SettingsService.SaveSettingsQuiet(settings);
            InputManager.LoadKeyBindings();

            await WavSounds.OK();
            await ShowSaveCompletionMessage();

            GoBack();
        }

        private async Task GoBackAsync()
        {
            await WavSounds.Cancel();

            GoBack();
        }

        #endregion

        #region Private Methods - 키 처리

        private static string NormalizeKeyName(Key key)
        {
            return key.ToString() switch
            {
                "LeftCtrl" => "LeftCtrl",
                "RightCtrl" => "RightCtrl",
                "LeftShift" => "LeftShift",
                "RightShift" => "RightShift",
                "LeftAlt" => "LeftAlt",
                "RightAlt" => "RightAlt",
                _ => key.ToString()
            };
        }

        private bool HasDuplicateKeys()
        {
            List<string> allKeys =  [
                DPadUp, DPadDown, DPadLeft, DPadRight,
                ButtonA, ButtonB, ButtonX, ButtonY,
                LeftBumper, RightBumper,
                LeftTrigger, RightTrigger ];

            var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in allKeys)
            {
                if (!uniqueKeys.Add(key)) return true;
            }

            return false;
        }

        private void AssignKeyToButton(string buttonName, string keyName)
        {
            switch (buttonName)
            {
                case "DPadUp": DPadUp = keyName; break;
                case "DPadDown": DPadDown = keyName; break;
                case "DPadLeft": DPadLeft = keyName; break;
                case "DPadRight": DPadRight = keyName; break;
                case "ButtonA": ButtonA = keyName; break;
                case "ButtonB": ButtonB = keyName; break;
                case "ButtonX": ButtonX = keyName; break;
                case "ButtonY": ButtonY = keyName; break;
                case "LeftBumper": LeftBumper = keyName; break;
                case "RightBumper": RightBumper = keyName; break;
                case "LeftTrigger": LeftTrigger = keyName; break;
                case "RightTrigger": RightTrigger = keyName; break;
                case "Start": Start = keyName; break;
                case "Select": Select = keyName; break;
            }
        }

        #endregion
    }
}