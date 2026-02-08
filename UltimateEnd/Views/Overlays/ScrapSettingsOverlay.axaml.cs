using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateEnd.Enums;
using UltimateEnd.Scraper.Models;
using UltimateEnd.Utils;

namespace UltimateEnd.Views.Overlays
{
    public partial class ScrapSettingsOverlay : BaseOverlay
    {
        private const string ChevronUpPath = "M 7 14 L 12 9 L 17 14";
        private const string ChevronDownPath = "M 7 10 L 12 15 L 17 10";

        public override bool Visible => MainGrid?.IsVisible ?? false;

        private string _tempLanguage = "jp";
        private string _tempRegionStyle = "Japanese";
        private LogoImageType _tempLogoImage;
        private CoverImageType _tempCoverImage;
        private bool _tempUseZipInternal;
        private bool _tempAllowScrapTitle;
        private bool _tempAllowScrapDescription;
        private bool _tempAllowScrapLogo;
        private bool _tempAllowScrapCover;
        private bool _tempAllowScrapVideo;
        private ScrapCondition _tempScrapCondition;
        private SearchMethod _tempSearchMethod;
        private bool _tempEnableAutoScrap;

        private bool _isLanguageExpanded = true;
        private bool _isRegionExpanded = true;
        private bool _isLogoTypeExpanded = true;
        private bool _isCoverTypeExpanded = true;
        private bool _isSearchMethodExpanded = true;
        private bool _isZipSearchExpanded = false;
        private bool _isScrapTargetsExpanded = false;
        private bool _isScrapConditionExpanded = false;
        private bool _isAdvancedExpanded = false;

        private int _selectedIndex = 0;
        private readonly List<Control> _focusableItems = [];

        public ScrapSettingsOverlay()
        {
            InitializeComponent();
            DelaySlider.ValueChanged += OnDelayChanged;
            HttpTimeoutSlider.ValueChanged += OnHttpTimeoutChanged;

            DelaySlider.AddHandler(KeyDownEvent, OnSliderKeyDown, RoutingStrategies.Tunnel);
            HttpTimeoutSlider.AddHandler(KeyDownEvent, OnSliderKeyDown, RoutingStrategies.Tunnel);
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            LoadSettings();

            this.IsVisible = true;

            if (MainGrid != null) MainGrid.IsVisible = true;

            Dispatcher.UIThread.Post(() =>
            {
                InitializeFocusableItems();
                UpdateSelection();
                this.Focusable = true;
                this.Focus();
            }, DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            if (MainGrid != null) MainGrid.IsVisible = false;

            this.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        protected override void MovePrevious()
        {
            if (_focusableItems.Count == 0) return;

            _selectedIndex = (_selectedIndex - 1 + _focusableItems.Count) % _focusableItems.Count;
            UpdateSelection(true);
            
            var selected = _focusableItems[_selectedIndex];

            if (selected is not TextBox && selected is not Slider) this.Focus();
        }

        protected override void MoveNext()
        {
            if (_focusableItems.Count == 0) return;

            _selectedIndex = (_selectedIndex + 1) % _focusableItems.Count;
            UpdateSelection(true);

            var selected = _focusableItems[_selectedIndex];

            if (selected is not TextBox && selected is not Slider) this.Focus();
        }

        protected override void SelectCurrent()
        {
            if (_focusableItems.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _focusableItems.Count) return;

            var selected = _focusableItems[_selectedIndex];

            if (selected is Border border)
                SimulateClick(border);
            else if (selected is Button button)
            {
                if (button.Content?.ToString() == "초기화")
                    OnResetClick(button, new RoutedEventArgs());
                else if (button.Content?.ToString() == "저장")
                    OnSaveClick(button, new RoutedEventArgs());
                else if (button.Content?.ToString() == "캐시 초기화")
                    OnClearCacheTapped(button, new RoutedEventArgs());
            }
        }

        private async void OnSliderKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not Slider slider) return;

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
        }

        private void InitializeFocusableItems()
        {
            _focusableItems.Clear();

            _focusableItems.Add(LanguageHeader);
            if (_isLanguageExpanded)
            {
                if (LanguageJpOption != null) _focusableItems.Add(LanguageJpOption);
                if (LanguageEnOption != null) _focusableItems.Add(LanguageEnOption);
            }

            _focusableItems.Add(RegionHeader);
            if (_isRegionExpanded)
            {
                if (RegionJapaneseOption != null) _focusableItems.Add(RegionJapaneseOption);
                if (RegionAmericanOption != null) _focusableItems.Add(RegionAmericanOption);
            }

            _focusableItems.Add(LogoTypeHeader);
            if (_isLogoTypeExpanded)
            {
                if (LogoNormalOption != null) _focusableItems.Add(LogoNormalOption);
                if (LogoSteelOption != null) _focusableItems.Add(LogoSteelOption);
            }

            _focusableItems.Add(CoverTypeHeader);
            if (_isCoverTypeExpanded)
            {
                if (Cover2DOption != null) _focusableItems.Add(Cover2DOption);
                if (Cover3DOption != null) _focusableItems.Add(Cover3DOption);
            }

            _focusableItems.Add(SearchMethodHeader);
            if (_isSearchMethodExpanded)
            {
                if (SearchByNameOption != null) _focusableItems.Add(SearchByNameOption);
                if (SearchByCrcOption != null) _focusableItems.Add(SearchByCrcOption);
            }

            _focusableItems.Add(ZipSearchHeader);
            if (_isZipSearchExpanded)
            {
                if (ZipInternalOption != null) _focusableItems.Add(ZipInternalOption);
                if (ZipExternalOption != null) _focusableItems.Add(ZipExternalOption);
            }

            _focusableItems.Add(ScrapTargetsHeader);
            if (_isScrapTargetsExpanded)
            {
                foreach (var child in ScrapTargetsContent.Children)
                    if (child is Border border) _focusableItems.Add(border);
            }

            _focusableItems.Add(ScrapConditionHeader);
            if (_isScrapConditionExpanded)
            {
                _focusableItems.Add(ConditionNoneOption);
                _focusableItems.Add(ConditionAllMissingOption);
                _focusableItems.Add(ConditionLogoMissingOption);
                _focusableItems.Add(ConditionCoverMissingOption);
                _focusableItems.Add(ConditionVideoMissingOption);
            }

            _focusableItems.Add(AdvancedHeader);
            if (_isAdvancedExpanded)
            {
                _focusableItems.Add(ClearCacheButton);
                _focusableItems.Add(UsernameTextBox);
                _focusableItems.Add(PasswordTextBox);
                _focusableItems.Add(DelaySlider);
                _focusableItems.Add(HttpTimeoutSlider);
                _focusableItems.Add(AutoScrapBorder);
            }

            _focusableItems.Add(ResetButton);
            _focusableItems.Add(SaveButton);

            if (_selectedIndex >= _focusableItems.Count)
                _selectedIndex = Math.Max(0, _focusableItems.Count - 1);
        }

        private void UpdateSelection(bool bringIntoView = false)
        {
            if (_focusableItems.Count == 0) return;

            for (int i = 0; i < _focusableItems.Count; i++)
            {
                var item = _focusableItems[i];

                if (i == _selectedIndex)
                {
                    if (item is Border border)
                    {
                        border.BorderBrush = this.FindResource("Accent.Blue") as IBrush;
                        border.BorderThickness = new Avalonia.Thickness(2);
                    }
                    else if (item is Button button)
                    {
                        button.BorderBrush = this.FindResource("Accent.Blue") as IBrush;
                        button.BorderThickness = new Avalonia.Thickness(2);
                        button.Opacity = 0.8;
                    }
                    else if (item is TextBox textBox)
                    {
                        textBox.BorderBrush = this.FindResource("Accent.Blue") as IBrush;
                        textBox.BorderThickness = new Avalonia.Thickness(2);
                        textBox.Focus();
                    }
                    else if (item is Slider slider)
                    {
                        slider.BorderBrush = this.FindResource("Accent.Blue") as IBrush;
                        slider.BorderThickness = new Avalonia.Thickness(2);
                        slider.Focus();
                    }

                    if (bringIntoView) item.BringIntoView();
                }
                else
                {
                    if (item is Border border)
                    {
                        border.BorderBrush = Avalonia.Media.Brushes.Transparent;
                        border.BorderThickness = new Avalonia.Thickness(0);
                    }
                    else if (item is Button button)
                    {
                        button.BorderBrush = null;
                        button.BorderThickness = new Avalonia.Thickness(0);
                        button.Opacity = 1.0;
                    }
                    else if (item is TextBox textBox)
                    {
                        textBox.BorderBrush = null;
                        textBox.BorderThickness = new Avalonia.Thickness(1);
                    }
                    else if (item is Slider slider)
                    {
                        slider.BorderBrush = null;
                        slider.BorderThickness = new Avalonia.Thickness(0);
                    }
                }
            }
        }

        private void SimulateClick(Border border)
        {
            if (border.Name == "LanguageHeader") { OnToggleLanguage(border, new RoutedEventArgs()); return; }
            if (border.Name == "RegionHeader") { OnToggleRegion(border, new RoutedEventArgs()); return; }
            if (border.Name == "LogoTypeHeader") { OnToggleLogoType(border, new RoutedEventArgs()); return; }
            if (border.Name == "CoverTypeHeader") { OnToggleCoverType(border, new RoutedEventArgs()); return; }
            if (border.Name == "SearchMethodHeader") { OnToggleSearchMethod(border, new RoutedEventArgs()); return; }
            if (border.Name == "ZipSearchHeader") { OnToggleZipSearch(border, new RoutedEventArgs()); return; }
            if (border.Name == "ScrapTargetsHeader") { OnToggleScrapTargets(border, new RoutedEventArgs()); return; }
            if (border.Name == "ScrapConditionHeader") { OnToggleScrapCondition(border, new RoutedEventArgs()); return; }
            if (border.Name == "AdvancedHeader") { OnToggleAdvanced(border, new RoutedEventArgs()); return; }

            if (border.Name?.Contains("Language") == true)
            {
                if (border.Name == "LanguageJpOption") OnLanguageJpTapped(border, new RoutedEventArgs());
                else if (border.Name == "LanguageEnOption") OnLanguageEnTapped(border, new RoutedEventArgs());
            }
            else if (border.Name?.Contains("Region") == true)
            {
                if (border.Name == "RegionJapaneseOption") OnRegionJapaneseTapped(border, new RoutedEventArgs());
                else if (border.Name == "RegionAmericanOption") OnRegionAmericanTapped(border, new RoutedEventArgs());
            }
            else if (border.Name?.Contains("Logo") == true)
            {
                if (border.Name == "LogoNormalOption") OnLogoNormalTapped(border, new RoutedEventArgs());
                else if (border.Name == "LogoSteelOption") OnLogoSteelTapped(border, new RoutedEventArgs());
            }
            else if (border.Name?.Contains("Cover") == true)
            {
                if (border.Name == "Cover2DOption") OnCover2DTapped(border, new RoutedEventArgs());
                else if (border.Name == "Cover3DOption") OnCover3DTapped(border, new RoutedEventArgs());
            }
            else if (border.Name?.Contains("Search") == true)
            {
                if (border.Name == "SearchByNameOption") OnSearchByNameTapped(border, new RoutedEventArgs());
                else if (border.Name == "SearchByCrcOption") OnSearchByCrcTapped(border, new RoutedEventArgs());
            }
            else if (border.Name?.Contains("Zip") == true)
            {
                if (border.Name == "ZipInternalOption") OnZipInternalTapped(border, new RoutedEventArgs());
                else if (border.Name == "ZipExternalOption") OnZipExternalTapped(border, new RoutedEventArgs());
            }
            else if (border.Name?.StartsWith("Condition") == true)
            {
                switch (border.Name)
                {
                    case "ConditionNoneOption": OnConditionNoneTapped(border, new RoutedEventArgs()); break;
                    case "ConditionAllMissingOption": OnConditionAllMissingTapped(border, new RoutedEventArgs()); break;
                    case "ConditionLogoMissingOption": OnConditionLogoMissingTapped(border, new RoutedEventArgs()); break;
                    case "ConditionCoverMissingOption": OnConditionCoverMissingTapped(border, new RoutedEventArgs()); break;
                    case "ConditionVideoMissingOption": OnConditionVideoMissingTapped(border, new RoutedEventArgs()); break;
                }
            }
            else
            {
                var textBlocks = border.GetVisualDescendants().OfType<TextBlock>().ToList();
                var firstText = textBlocks.FirstOrDefault()?.Text;

                if (firstText != null)
                {
                    switch (firstText)
                    {
                        case "게임명": OnToggleScrapTitle(border, new RoutedEventArgs()); break;
                        case "게임 설명": OnToggleScrapDescription(border, new RoutedEventArgs()); break;
                        case "로고": OnToggleScrapLogo(border, new RoutedEventArgs()); break;
                        case "커버": OnToggleScrapCover(border, new RoutedEventArgs()); break;
                        case "비디오": OnToggleScrapVideo(border, new RoutedEventArgs()); break;
                        case "자동 스크래핑 활성화": OnToggleAutoScrap(border, new RoutedEventArgs()); break;
                    }
                }
            }
        }

        private void LoadSettings()
        {
            var config = ScreenScraperConfig.Instance;

            _tempLanguage = config.PreferredLanguage;
            _tempRegionStyle = config.RegionStyle;
            _tempLogoImage = config.LogoImage;
            _tempCoverImage = config.CoverImage;
            _tempUseZipInternal = config.UseZipInternalFileName;
            _tempAllowScrapTitle = config.AllowScrapTitle;
            _tempAllowScrapDescription = config.AllowScrapDescription;
            _tempAllowScrapLogo = config.AllowScrapLogo;
            _tempAllowScrapCover = config.AllowScrapCover;
            _tempAllowScrapVideo = config.AllowScrapVideo;
            _tempScrapCondition = config.ScrapConditionType;
            _tempEnableAutoScrap = config.EnableAutoScrap;
            _tempSearchMethod = config.PreferredSearchMethod;

            UpdateLanguageUI();
            UpdateRegionUI();
            UpdateLogoImageUI();
            UpdateCoverImageUI();
            UpdateZipSearchUI();
            UpdateScrapTargetsUI();
            UpdateScrapConditionUI();
            UpdateExpandersUI();
            UpdateAutoScrapUI();
            UpdateSearchMethodUI();

            UsernameTextBox.Text = config.Username;
            PasswordTextBox.Text = config.Password;
            DelaySlider.Value = config.DelayBetweenRequestsMs;
            DelayValueText.Text = $"{config.DelayBetweenRequestsMs}ms";
            HttpTimeoutSlider.Value = config.HttpTimeoutSeconds;
            HttpTimeoutValueText.Text = $"{config.HttpTimeoutSeconds}초";
        }

        private void UpdateLanguageUI()
        {
            LanguageEnCheck.IsVisible = _tempLanguage == "en";
            LanguageJpCheck.IsVisible = _tempLanguage == "jp";
        }

        private void UpdateRegionUI()
        {
            RegionJapaneseCheck.IsVisible = _tempRegionStyle == "Japanese";
            RegionAmericanCheck.IsVisible = _tempRegionStyle == "American";
        }

        private void UpdateLogoImageUI()
        {
            LogoNormalCheck.IsVisible = _tempLogoImage == LogoImageType.Normal;
            LogoSteelCheck.IsVisible = _tempLogoImage == LogoImageType.Steel;
        }

        private void UpdateCoverImageUI()
        {
            Cover3DCheck.IsVisible = _tempCoverImage == CoverImageType.Box3D;
            Cover2DCheck.IsVisible = _tempCoverImage == CoverImageType.Box2D;
        }

        private void UpdateZipSearchUI()
        {
            ZipInternalCheck.IsVisible = _tempUseZipInternal;
            ZipExternalCheck.IsVisible = !_tempUseZipInternal;
        }

        private void UpdateScrapTargetsUI()
        {
            UpdateToggle(ScrapTitleToggleBack, ScrapTitleToggle, _tempAllowScrapTitle);
            UpdateToggle(ScrapDescriptionToggleBack, ScrapDescriptionToggle, _tempAllowScrapDescription);
            UpdateToggle(ScrapLogoToggleBack, ScrapLogoToggle, _tempAllowScrapLogo);
            UpdateToggle(ScrapCoverToggleBack, ScrapCoverToggle, _tempAllowScrapCover);
            UpdateToggle(ScrapVideoToggleBack, ScrapVideoToggle, _tempAllowScrapVideo);
        }

        private void UpdateScrapConditionUI()
        {
            ConditionNoneCheck.IsVisible = _tempScrapCondition == ScrapCondition.None;
            ConditionAllMissingCheck.IsVisible = _tempScrapCondition == ScrapCondition.AllMediaMissing;
            ConditionLogoMissingCheck.IsVisible = _tempScrapCondition == ScrapCondition.LogoMissing;
            ConditionCoverMissingCheck.IsVisible = _tempScrapCondition == ScrapCondition.CoverMissing;
            ConditionVideoMissingCheck.IsVisible = _tempScrapCondition == ScrapCondition.VideoMissing;
        }

        private void UpdateAutoScrapUI() => UpdateToggle(AutoScrapToggleBack, AutoScrapToggle, _tempEnableAutoScrap);

        private void UpdateSearchMethodUI()
        {
            SearchByNameCheck.IsVisible = _tempSearchMethod == SearchMethod.ByFileName;
            SearchByCrcCheck.IsVisible = _tempSearchMethod == SearchMethod.ByCrc;
        }

        private void UpdateExpandersUI()
        {
            LanguageContent.IsVisible = _isLanguageExpanded;
            LanguageExpandIcon.Data = Geometry.Parse(_isLanguageExpanded ? ChevronUpPath : ChevronDownPath);

            RegionContent.IsVisible = _isRegionExpanded;
            RegionExpandIcon.Data = Geometry.Parse(_isRegionExpanded ? ChevronUpPath : ChevronDownPath);

            LogoTypeContent.IsVisible = _isLogoTypeExpanded;
            LogoTypeExpandIcon.Data = Geometry.Parse(_isLogoTypeExpanded ? ChevronUpPath : ChevronDownPath);

            CoverTypeContent.IsVisible = _isCoverTypeExpanded;
            CoverTypeExpandIcon.Data = Geometry.Parse(_isCoverTypeExpanded ? ChevronUpPath : ChevronDownPath);

            SearchMethodContent.IsVisible = _isSearchMethodExpanded;
            SearchMethodExpandIcon.Data = Geometry.Parse(_isSearchMethodExpanded ? ChevronUpPath : ChevronDownPath);

            ZipSearchContent.IsVisible = _isZipSearchExpanded;
            ZipSearchExpandIcon.Data = Geometry.Parse(_isZipSearchExpanded ? ChevronUpPath : ChevronDownPath);

            ScrapTargetsContent.IsVisible = _isScrapTargetsExpanded;
            ScrapTargetsExpandIcon.Data = Geometry.Parse(_isScrapTargetsExpanded ? ChevronUpPath : ChevronDownPath);

            ScrapConditionContent.IsVisible = _isScrapConditionExpanded;
            ScrapConditionExpandIcon.Data = Geometry.Parse(_isScrapConditionExpanded ? ChevronUpPath : ChevronDownPath);

            AdvancedContent.IsVisible = _isAdvancedExpanded;
            AdvancedExpandIcon.Data = Geometry.Parse(_isAdvancedExpanded ? ChevronUpPath : ChevronDownPath);

            InitializeFocusableItems();
            UpdateSelection();
        }

        private static void UpdateToggle(Border toggleBack, Border toggle, bool value)
        {
            string resourceKey = value ? "Toggle.SelectionBackground" : "Toggle.Background";

            if (Avalonia.Application.Current != null && Avalonia.Application.Current.Resources.TryGetResource(resourceKey, Avalonia.Application.Current?.ActualThemeVariant, out object? resourceObj)) toggleBack.Background = resourceObj as IBrush;

            toggle.HorizontalAlignment = value ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        }

        private void SaveSettings()
        {
            try
            {
                var config = ScreenScraperConfig.Instance;

                config.PreferredLanguage = _tempLanguage;
                config.RegionStyle = _tempRegionStyle;
                config.LogoImage = _tempLogoImage;
                config.CoverImage = _tempCoverImage;
                config.UseZipInternalFileName = _tempUseZipInternal;
                config.AllowScrapTitle = _tempAllowScrapTitle;
                config.AllowScrapDescription = _tempAllowScrapDescription;
                config.AllowScrapLogo = _tempAllowScrapLogo;
                config.AllowScrapCover = _tempAllowScrapCover;
                config.AllowScrapVideo = _tempAllowScrapVideo;
                config.ScrapConditionType = _tempScrapCondition;
                config.EnableAutoScrap = _tempEnableAutoScrap;
                config.Username = UsernameTextBox.Text ?? string.Empty;
                config.Password = PasswordTextBox.Text ?? string.Empty;
                config.DelayBetweenRequestsMs = (int)DelaySlider.Value;
                config.HttpTimeoutSeconds = (int)HttpTimeoutSlider.Value;
                config.PreferredSearchMethod = _tempSearchMethod;

                config.Save();

                Hide(HiddenState.Confirm);
            }
            catch { }
        }

        private void OnBackClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            Hide(HiddenState.Cancel);
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender) Hide(HiddenState.Cancel);
        }

        private async void OnToggleLanguage(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isLanguageExpanded = !_isLanguageExpanded;
            UpdateExpandersUI();
        }

        private async void OnLanguageJpTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempLanguage = "jp";
            UpdateLanguageUI();
        }

        private async void OnLanguageEnTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempLanguage = "en";
            UpdateLanguageUI();
        }

        private async void OnToggleRegion(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isRegionExpanded = !_isRegionExpanded;
            UpdateExpandersUI();
        }

        private async void OnRegionJapaneseTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempRegionStyle = "Japanese";
            UpdateRegionUI();
        }

        private async void OnRegionAmericanTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempRegionStyle = "American";
            UpdateRegionUI();
        }

        private async void OnToggleLogoType(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isLogoTypeExpanded = !_isLogoTypeExpanded;
            UpdateExpandersUI();
        }

        private async void OnLogoNormalTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempLogoImage = LogoImageType.Normal;
            UpdateLogoImageUI();
        }

        private async void OnLogoSteelTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempLogoImage = LogoImageType.Steel;
            UpdateLogoImageUI();
        }

        private async void OnToggleCoverType(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isCoverTypeExpanded = !_isCoverTypeExpanded;
            UpdateExpandersUI();
        }

        private async void OnCover3DTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempCoverImage = CoverImageType.Box3D;
            UpdateCoverImageUI();
        }

        private async void OnCover2DTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempCoverImage = CoverImageType.Box2D;
            UpdateCoverImageUI();
        }

        private async void OnToggleZipSearch(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isZipSearchExpanded = !_isZipSearchExpanded;
            UpdateExpandersUI();
        }

        private async void OnZipInternalTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempUseZipInternal = true;
            UpdateZipSearchUI();
        }

        private async void OnZipExternalTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempUseZipInternal = false;
            UpdateZipSearchUI();
        }

        private async void OnToggleScrapTitle(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapTitle = !_tempAllowScrapTitle;
            UpdateScrapTargetsUI();
        }

        private async void OnToggleScrapDescription(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapDescription = !_tempAllowScrapDescription;
            UpdateScrapTargetsUI();
        }

        private async void OnToggleScrapLogo(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapLogo = !_tempAllowScrapLogo;
            UpdateScrapTargetsUI();
        }

        private async void OnToggleScrapCover(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapCover = !_tempAllowScrapCover;
            UpdateScrapTargetsUI();
        }

        private async void OnToggleScrapVideo(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapVideo = !_tempAllowScrapVideo;
            UpdateScrapTargetsUI();
        }

        private async void OnConditionNoneTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.None;
            UpdateScrapConditionUI();
        }

        private async void OnConditionAllMissingTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.AllMediaMissing;
            UpdateScrapConditionUI();
        }

        private async void OnConditionLogoMissingTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.LogoMissing;
            UpdateScrapConditionUI();
        }

        private async void OnConditionCoverMissingTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.CoverMissing;
            UpdateScrapConditionUI();
        }

        private async void OnConditionVideoMissingTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.VideoMissing;
            UpdateScrapConditionUI();
        }

        private async void OnToggleScrapTargets(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isScrapTargetsExpanded = !_isScrapTargetsExpanded;
            UpdateExpandersUI();
        }

        private async void OnToggleScrapCondition(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isScrapConditionExpanded = !_isScrapConditionExpanded;
            UpdateExpandersUI();
        }

        private async void OnToggleAdvanced(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isAdvancedExpanded = !_isAdvancedExpanded;
            UpdateExpandersUI();
        }

        private void OnDelayChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (DelayValueText != null) DelayValueText.Text = $"{(int)e.NewValue}ms";
        }

        private void OnHttpTimeoutChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (HttpTimeoutValueText != null) HttpTimeoutValueText.Text = $"{(int)e.NewValue}초";
        }

        private async void OnClearCacheTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var result = await Services.DialogService.Instance.ShowConfirm("캐시를 초기화하시겠습니까?", "모든 스크랩 캐시가 삭제되며, 다음 검색 시 API를 다시 호출합니다.");

            if (result)
            {
                try
                {
                    Scraper.ScreenScraperCache.ClearCache();
                    await Services.DialogService.Instance.ShowSuccess("캐시가 초기화되었습니다.");
                }
                catch (Exception ex)
                {
                    await Services.DialogService.Instance.ShowError($"캐시 초기화 실패: {ex.Message}");
                }
            }
        }

        private async void OnResetClick(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;

            var result = await Services.DialogService.Instance.ShowConfirm("설정을 초기화하시겠습니까?", "모든 설정이 기본값으로 되돌아갑니다.");

            if (result)
            {
                await WavSounds.Confirm();
                ScreenScraperConfig.Instance.Reset();
                LoadSettings();
            }
        }

        private async void OnToggleAutoScrap(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempEnableAutoScrap = !_tempEnableAutoScrap;
            UpdateAutoScrapUI();
        }

        private async void OnToggleSearchMethod(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isSearchMethodExpanded = !_isSearchMethodExpanded;
            UpdateExpandersUI();
        }

        private async void OnSearchByNameTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempSearchMethod = SearchMethod.ByFileName;
            UpdateSearchMethodUI();
        }

        private async void OnSearchByCrcTapped(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempSearchMethod = SearchMethod.ByCrc;
            UpdateSearchMethodUI();
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            e.Handled = true;
            SaveSettings();
        }
    }
}