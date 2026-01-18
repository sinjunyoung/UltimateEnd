using ReactiveUI;
using System.Collections.Generic;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Desktop.ViewModels
{
    public class KeyBindingSettingsViewModel : KeyBindingSettingsViewModelBase
    {
        private string _controllerType = "Auto";

        public KeyBindingSettingsViewModel() : base()
        {
        }

        #region Properties

        public string ControllerType
        {
            get => _controllerType;
            set => this.RaiseAndSetIfChanged(ref _controllerType, value);
        }

        public List<string> ControllerTypes { get; } = ["Auto", "Xbox", "PlayStation", "Switch"];

        #endregion

        #region Override - 플랫폼별 확장

        protected override void LoadPlatformSpecificSettings(AppSettings settings)
        {
            ControllerType = settings.ControllerType ?? "Auto";
        }

        protected override void SavePlatformSpecificSettings(AppSettings settings)
        {
            settings.ControllerType = ControllerType;
        }

        protected override void ResetPlatformSpecificDefaults()
        {
            ControllerType = "Auto";
        }

        protected override async Task ShowSaveCompletionMessage()
        {
            var currentSettings = SettingsService.LoadSettings();

            if (currentSettings.ControllerType != "Auto")
                await DialogService.Instance.ShowInfo("설정을 저장했습니다.\n" + "컨트롤러 타입 변경은 프로그램 재시작 후 적용됩니다.");
        }

        protected override string GetButtonDisplayName(string buttonName)
        {
            return buttonName switch
            {
                "DPadUp" => "↑ (D-Pad Up)",
                "DPadDown" => "↓ (D-Pad Down)",
                "DPadLeft" => "← (D-Pad Left)",
                "DPadRight" => "→ (D-Pad Right)",
                "ButtonA" => GetControllerButtonName("A"),
                "ButtonB" => GetControllerButtonName("B"),
                "ButtonX" => GetControllerButtonName("X"),
                "ButtonY" => GetControllerButtonName("Y"),
                "LeftBumper" => "LB (Left Bumper)",
                "RightBumper" => "RB (Right Bumper)",
                "LeftTrigger" => "LT (Left Trigger)",
                "RightTrigger" => "RT (Right Trigger)",
                "Start" => GetControllerStartButton(),
                "Select" => GetControllerSelectButton(),
                _ => buttonName
            };
        }

        private string GetControllerButtonName(string button)
        {
            return ControllerType switch
            {
                "PlayStation" => button switch
                {
                    "A" => "✕ (Cross)",
                    "B" => "◯ (Circle)",
                    "X" => "□ (Square)",
                    "Y" => "△ (Triangle)",
                    _ => $"{button} 버튼"
                },
                "Switch" => button switch
                {
                    "A" => "B 버튼 (하단)",
                    "B" => "A 버튼 (우측)",
                    "X" => "Y 버튼 (좌측)",
                    "Y" => "X 버튼 (상단)",
                    _ => $"{button} 버튼"
                },
                _ => $"{button} 버튼" // Xbox 또는 Auto
            };
        }

        private string GetControllerStartButton()
        {
            return ControllerType switch
            {
                "PlayStation" => "Options",
                "Switch" => "+ (Plus)",
                _ => "Start"
            };
        }

        private string GetControllerSelectButton()
        {
            return ControllerType switch
            {
                "PlayStation" => "Share / Create",
                "Switch" => "- (Minus)",
                _ => "Select / Back"
            };
        }

        #endregion
    }
}