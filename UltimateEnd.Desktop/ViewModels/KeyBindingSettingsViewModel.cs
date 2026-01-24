using ReactiveUI;
using System.Collections.Generic;
using UltimateEnd.Desktop.Services;
using UltimateEnd.Models;
using UltimateEnd.Services;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Desktop.ViewModels
{
    public class KeyBindingSettingsViewModel : KeyBindingSettingsViewModelBase
    {
        #region Fields

        private bool _isGamepadMode = false;
        private bool _isGamepadConnected = false;
        private string _detectedControllerType = "Xbox";

        private int _gamepadButtonA = 0;
        private int _gamepadButtonB = 1;
        private int _gamepadButtonX = 2;
        private int _gamepadButtonY = 3;
        private int _gamepadLeftBumper = 4;
        private int _gamepadRightBumper = 5;
        private int _gamepadSelect = 6;
        private int _gamepadStart = 7;

        private List<string> _connectedGamepads = [];
        private int _selectedGamepadIndex = 0;

        #endregion

        #region Properties - Gamepad State

        public bool IsGamepadMode
        {
            get => _isGamepadMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _isGamepadMode, value);
                this.RaisePropertyChanged(nameof(IsKeyboardMode));
            }
        }

        public bool IsGamepadConnected
        {
            get => _isGamepadConnected;
            set => this.RaiseAndSetIfChanged(ref _isGamepadConnected, value);
        }

        public string DetectedControllerType
        {
            get => _detectedControllerType;
            set
            {
                this.RaiseAndSetIfChanged(ref _detectedControllerType, value);
                this.RaisePropertyChanged(nameof(GamepadButtonText));
            }
        }
               
        public bool IsKeyboardMode => !_isGamepadMode;

        public List<string> ConnectedGamepads
        {
            get => _connectedGamepads;
            set
            {
                this.RaiseAndSetIfChanged(ref _connectedGamepads, value);
                this.RaisePropertyChanged(nameof(HasMultipleGamepads));
            }
        }

        public int SelectedGamepadIndex
        {
            get => _selectedGamepadIndex;
            set
            {
                if (_selectedGamepadIndex != value)
                {
                    this.RaiseAndSetIfChanged(ref _selectedGamepadIndex, value);
                    GamepadManager.SetActiveGamepad(value);

                    DetectedControllerType = GamepadManager.GetDetectedControllerType();

                    this.RaisePropertyChanged(nameof(SelectedGamepadName));
                    this.RaisePropertyChanged(nameof(GamepadButtonText));

                    LoadPlatformSpecificSettings(SettingsService.LoadSettings());
                }
            }
        }

        public string SelectedGamepadName => ConnectedGamepads != null && SelectedGamepadIndex >= 0 && SelectedGamepadIndex < ConnectedGamepads.Count ? ConnectedGamepads[SelectedGamepadIndex] : string.Empty;

        public bool HasMultipleGamepads => ConnectedGamepads?.Count > 1;

        #endregion

        #region Properties - Gamepad Button Mappings

        public int GamepadButtonA
        {
            get => _gamepadButtonA;
            set
            {
                this.RaiseAndSetIfChanged(ref _gamepadButtonA, value);
                this.RaisePropertyChanged(nameof(GamepadButtonADisplay));
            }
        }

        public int GamepadButtonB
        {
            get => _gamepadButtonB;
            set
            {
                this.RaiseAndSetIfChanged(ref _gamepadButtonB, value);
                this.RaisePropertyChanged(nameof(GamepadButtonBDisplay));
            }
        }

        public int GamepadButtonX
        {
            get => _gamepadButtonX;
            set
            {
                this.RaiseAndSetIfChanged(ref _gamepadButtonX, value);
                this.RaisePropertyChanged(nameof(GamepadButtonXDisplay));
            }
        }

        public int GamepadButtonY
        {
            get => _gamepadButtonY;
            set
            {
                this.RaiseAndSetIfChanged(ref _gamepadButtonY, value);
                this.RaisePropertyChanged(nameof(GamepadButtonYDisplay));
            }
        }

        public int GamepadLeftBumper
        {
            get => _gamepadLeftBumper;
            set
            {
                this.RaiseAndSetIfChanged(ref _gamepadLeftBumper, value);
                this.RaisePropertyChanged(nameof(GamepadLeftBumperDisplay));
            }
        }

        public int GamepadRightBumper
        {
            get => _gamepadRightBumper;
            set
            {
                this.RaiseAndSetIfChanged(ref _gamepadRightBumper, value);
                this.RaisePropertyChanged(nameof(GamepadRightBumperDisplay));
            }
        }

        public int GamepadSelect
        {
            get => _gamepadSelect;
            set => this.RaiseAndSetIfChanged(ref _gamepadSelect, value);
        }

        public int GamepadStart
        {
            get => _gamepadStart;
            set => this.RaiseAndSetIfChanged(ref _gamepadStart, value);
        }

        public string GamepadButtonText => $"게임패드 ({GamepadManager.GetDetectedControllerName()})";

        #endregion

        #region Properties - Gamepad Button Display

        public string GamepadButtonADisplay => GetGamepadButtonName(GamepadButtonA);

        public string GamepadButtonBDisplay => GetGamepadButtonName(GamepadButtonB);

        public string GamepadButtonXDisplay => GetGamepadButtonName(GamepadButtonX);

        public string GamepadButtonYDisplay => GetGamepadButtonName(GamepadButtonY);

        public string GamepadLeftBumperDisplay => GetGamepadButtonName(GamepadLeftBumper);

        public string GamepadRightBumperDisplay => GetGamepadButtonName(GamepadRightBumper);

        #endregion

        #region Commands

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SwitchToKeyboardCommand { get; }

        public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SwitchToGamepadCommand { get; }

        #endregion

        #region Constructor

        public KeyBindingSettingsViewModel() : base()
        {
            UpdateGamepadConnectionStatus();

            SwitchToKeyboardCommand = ReactiveCommand.Create(() =>
            {
                IsGamepadMode = false;
            });

            SwitchToGamepadCommand = ReactiveCommand.Create(() =>
            {
                IsGamepadMode = true;
            });
        }

        #endregion

        #region Public Methods

        public override void StartBinding(string buttonName)
        {
            base.StartBinding(buttonName);

            GamepadManager.SetBindingMode(true);
        }

        public void HandleGamepadButtonPress(int buttonIndex)
        {
            if (!IsBinding || string.IsNullOrEmpty(_currentBindingKey)) return;

            AssignGamepadButtonToButton(_currentBindingKey, buttonIndex);

            IsBinding = false;
            _currentBindingKey = string.Empty;

            GamepadManager.SetBindingMode(false);
        }

        public void UpdateGamepadConnectionStatus()
        {
            GamepadManager.RefreshDevices();

            IsGamepadConnected = GamepadManager.IsGamepadConnected();

            if (IsGamepadConnected)
            {
                DetectedControllerType = GamepadManager.GetDetectedControllerType();

                ConnectedGamepads = GamepadManager.GetGamepadNames();
                SelectedGamepadIndex = GamepadManager.GetActiveGamepadIndex();

                if (SelectedGamepadIndex >= ConnectedGamepads.Count) SelectedGamepadIndex = 0;

                this.RaisePropertyChanged(nameof(SelectedGamepadName));
            }
            else
            {
                ConnectedGamepads.Clear();
                SelectedGamepadIndex = 0;

                if (IsGamepadMode) IsGamepadMode = false;
            }
        }

        #endregion

        #region Protected Override Methods

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

        protected override void LoadPlatformSpecificSettings(AppSettings settings)
        {
            if (settings.GamepadButtonMapping != null && settings.GamepadButtonMapping.Count > 0)
            {
                GamepadButtonA = settings.GamepadButtonMapping.GetValueOrDefault("ButtonA", 0);
                GamepadButtonB = settings.GamepadButtonMapping.GetValueOrDefault("ButtonB", 1);
                GamepadButtonX = settings.GamepadButtonMapping.GetValueOrDefault("ButtonX", 2);
                GamepadButtonY = settings.GamepadButtonMapping.GetValueOrDefault("ButtonY", 3);
                GamepadLeftBumper = settings.GamepadButtonMapping.GetValueOrDefault("LeftBumper", 4);
                GamepadRightBumper = settings.GamepadButtonMapping.GetValueOrDefault("RightBumper", 5);
                GamepadSelect = settings.GamepadButtonMapping.GetValueOrDefault("Select", 6);
                GamepadStart = settings.GamepadButtonMapping.GetValueOrDefault("Start", 7);
            }
            else
            {
                DetectedControllerType = GetDetectedControllerType();

                switch (DetectedControllerType)
                {
                    case "PlayStation":
                        GamepadButtonA = 1;
                        GamepadButtonB = 2;
                        GamepadButtonX = 0;
                        GamepadButtonY = 3;
                        GamepadLeftBumper = 4;
                        GamepadRightBumper = 5;
                        GamepadSelect = 8;
                        GamepadStart = 9;
                        break;

                    case "Switch":
                        GamepadButtonA = 1;
                        GamepadButtonB = 0;
                        GamepadButtonX = 3;
                        GamepadButtonY = 2;
                        GamepadLeftBumper = 4;
                        GamepadRightBumper = 5;
                        GamepadSelect = 8;
                        GamepadStart = 9;
                        break;

                    default:
                        GamepadButtonA = 0;
                        GamepadButtonB = 1;
                        GamepadButtonX = 2;
                        GamepadButtonY = 3;
                        GamepadLeftBumper = 4;
                        GamepadRightBumper = 5;
                        GamepadSelect = 6;
                        GamepadStart = 7;
                        break;
                }
            }
        }

        protected override void SavePlatformSpecificSettings(AppSettings settings)
        {
            settings.GamepadButtonMapping ??= [];

            settings.GamepadButtonMapping["ButtonA"] = GamepadButtonA;
            settings.GamepadButtonMapping["ButtonB"] = GamepadButtonB;
            settings.GamepadButtonMapping["ButtonX"] = GamepadButtonX;
            settings.GamepadButtonMapping["ButtonY"] = GamepadButtonY;
            settings.GamepadButtonMapping["LeftBumper"] = GamepadLeftBumper;
            settings.GamepadButtonMapping["RightBumper"] = GamepadRightBumper;
            settings.GamepadButtonMapping["Select"] = GamepadSelect;
            settings.GamepadButtonMapping["Start"] = GamepadStart;

            GamepadManager.ReloadButtonMapping();
        }

        protected override void ResetPlatformSpecificDefaults()
        {
            DetectedControllerType = GetDetectedControllerType();

            switch (DetectedControllerType)
            {
                case "PlayStation":
                    GamepadButtonA = 1;
                    GamepadButtonB = 2;
                    GamepadButtonX = 0;
                    GamepadButtonY = 3;
                    GamepadLeftBumper = 4;
                    GamepadRightBumper = 5;
                    GamepadSelect = 8;
                    GamepadStart = 9;
                    break;

                case "Switch":
                    GamepadButtonA = 1;
                    GamepadButtonB = 0;
                    GamepadButtonX = 3;
                    GamepadButtonY = 2;
                    GamepadLeftBumper = 4;
                    GamepadRightBumper = 5;
                    GamepadSelect = 8;
                    GamepadStart = 9;
                    break;

                default:
                    GamepadButtonA = 0;
                    GamepadButtonB = 1;
                    GamepadButtonX = 2;
                    GamepadButtonY = 3;
                    GamepadLeftBumper = 4;
                    GamepadRightBumper = 5;
                    GamepadSelect = 6;
                    GamepadStart = 7;
                    break;
            }
        }

        protected override bool HasDuplicateGamepadButtons()
        {
            if (!IsGamepadMode) return false;

            List<int> allButtons = [GamepadButtonA, GamepadButtonB, GamepadButtonX, GamepadButtonY, GamepadLeftBumper, GamepadRightBumper];

            var uniqueButtons = new HashSet<int>();

            foreach (var button in allButtons)
            {
                if (!uniqueButtons.Add(button)) return true;
            }

            return false;
        }

        #endregion

        #region Private Methods        

        private static string GetDetectedControllerType()
        {
            if (!GamepadManager.IsGamepadConnected()) return "Xbox";

            return GamepadManager.GetDetectedControllerType();
        }

        private string GetControllerButtonName(string button)
        {
            return DetectedControllerType switch
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
                _ => $"{button} 버튼"
            };
        }

        private string GetControllerStartButton()
        {
            return DetectedControllerType switch
            {
                "PlayStation" => "Options",
                "Switch" => "+ (Plus)",
                _ => "Start"
            };
        }

        private string GetControllerSelectButton()
        {
            return DetectedControllerType switch
            {
                "PlayStation" => "Share / Create",
                "Switch" => "- (Minus)",
                _ => "Select / Back"
            };
        }

        private string GetGamepadButtonName(int buttonIndex)
        {
            return DetectedControllerType switch
            {
                "PlayStation" => buttonIndex switch
                {
                    0 => "□",
                    1 => "✕",
                    2 => "◯",
                    3 => "△",
                    4 => "L1",
                    5 => "R1",
                    6 => "L2",
                    7 => "R2",
                    8 => "Share",
                    9 => "Options",
                    _ => buttonIndex.ToString()
                },
                "Switch" => buttonIndex switch
                {
                    0 => "Y",
                    1 => "B",
                    2 => "A",
                    3 => "X",
                    4 => "L",
                    5 => "R",
                    6 => "ZL",
                    7 => "ZR",
                    8 => "-",
                    9 => "+",
                    _ => buttonIndex.ToString()
                },
                _ => buttonIndex switch
                {
                    0 => "A",
                    1 => "B",
                    2 => "X",
                    3 => "Y",
                    4 => "LB",
                    5 => "RB",
                    6 => "Back",
                    7 => "Start",
                    _ => buttonIndex.ToString()
                }
            };
        }

        private void AssignGamepadButtonToButton(string buttonName, int buttonIndex)
        {
            switch (buttonName)
            {
                case "ButtonA": GamepadButtonA = buttonIndex; break;
                case "ButtonB": GamepadButtonB = buttonIndex; break;
                case "ButtonX": GamepadButtonX = buttonIndex; break;
                case "ButtonY": GamepadButtonY = buttonIndex; break;
                case "LeftBumper": GamepadLeftBumper = buttonIndex; break;
                case "RightBumper": GamepadRightBumper = buttonIndex; break;
                case "Start": GamepadStart = buttonIndex; break;
                case "Select": GamepadSelect = buttonIndex; break;
            }
        }

        #endregion
    }
}