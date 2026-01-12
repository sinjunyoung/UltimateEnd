using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
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

        private bool _isLanguageExpanded = true;
        private bool _isRegionExpanded = true;
        private bool _isLogoTypeExpanded = true;
        private bool _isCoverTypeExpanded = true;
        private bool _isZipSearchExpanded = false;
        private bool _isScrapTargetsExpanded = false;
        private bool _isScrapConditionExpanded = false;
        private bool _isAdvancedExpanded = false;
        private bool _tempEnableAutoScrap;        
        private bool _isSearchMethodExpanded = true;

        public ScrapSettingsOverlay()
        {
            InitializeComponent();
            DelaySlider.ValueChanged += OnDelayChanged;
            HttpTimeoutSlider.ValueChanged += OnHttpTimeoutChanged;

            if (OperatingSystem.IsAndroid())
                 SearchMethodSection.IsVisible = false;

        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            LoadSettings();

            this.IsVisible = true;
            if (MainGrid != null) MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();
        }

        public override void Hide(HiddenState state)
        {
            if (MainGrid != null) MainGrid.IsVisible = false;
            this.IsVisible = false;
            OnHidden(new HiddenEventArgs { State = state });
        }

        protected override void MovePrevious() { }
        protected override void MoveNext() { }
        protected override void SelectCurrent() { }

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
        }

        private static void UpdateToggle(Border toggleBack, Border toggle, bool value)
        {
            string resourceKey = value ? "Toggle.SelectionBackground" : "Toggle.Background";

            if (Avalonia.Application.Current != null && Avalonia.Application.Current.Resources.TryGetResource(resourceKey, Avalonia.Application.Current?.ActualThemeVariant, out object? resourceObj))
                toggleBack.Background = resourceObj as IBrush;

            toggle.HorizontalAlignment = value ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        }

        private async void SaveSettings()
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

                await WavSounds.Confirm();
                Hide(HiddenState.Confirm);
            }
            catch { }
        }

        private async void OnBackClick(object? sender, PointerPressedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Cancel();
            Hide(HiddenState.Cancel);
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
            {
                _ = WavSounds.Cancel();
                Hide(HiddenState.Cancel);
            }
        }

        private async void OnToggleLanguage(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isLanguageExpanded = !_isLanguageExpanded;
            UpdateExpandersUI();
        }

        private async void OnLanguageJpTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempLanguage = "jp";
            UpdateLanguageUI();
        }

        private async void OnLanguageEnTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempLanguage = "en";
            UpdateLanguageUI();
        }

        private async void OnToggleRegion(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isRegionExpanded = !_isRegionExpanded;
            UpdateExpandersUI();
        }

        private async void OnRegionJapaneseTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempRegionStyle = "Japanese";
            UpdateRegionUI();
        }

        private async void OnRegionAmericanTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempRegionStyle = "American";
            UpdateRegionUI();
        }

        private async void OnToggleLogoType(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isLogoTypeExpanded = !_isLogoTypeExpanded;
            UpdateExpandersUI();
        }

        private async void OnLogoNormalTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempLogoImage = LogoImageType.Normal;
            UpdateLogoImageUI();
        }

        private async void OnLogoSteelTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempLogoImage = LogoImageType.Steel;
            UpdateLogoImageUI();
        }

        private async void OnToggleCoverType(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isCoverTypeExpanded = !_isCoverTypeExpanded;
            UpdateExpandersUI();
        }

        private async void OnCover3DTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempCoverImage = CoverImageType.Box3D;
            UpdateCoverImageUI();
        }

        private async void OnCover2DTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempCoverImage = CoverImageType.Box2D;
            UpdateCoverImageUI();
        }

        private async void OnToggleZipSearch(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isZipSearchExpanded = !_isZipSearchExpanded;
            UpdateExpandersUI();
        }

        private async void OnZipInternalTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempUseZipInternal = true;
            UpdateZipSearchUI();
        }

        private async void OnZipExternalTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempUseZipInternal = false;
            UpdateZipSearchUI();
        }

        private async void OnToggleScrapTitle(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapTitle = !_tempAllowScrapTitle;
            UpdateScrapTargetsUI();
        }

        private async void OnToggleScrapDescription(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapDescription = !_tempAllowScrapDescription;
            UpdateScrapTargetsUI();
        }

        private async void OnToggleScrapLogo(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapLogo = !_tempAllowScrapLogo;
            UpdateScrapTargetsUI();
        }

        private async void OnToggleScrapCover(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapCover = !_tempAllowScrapCover;
            UpdateScrapTargetsUI();
        }

        private async void OnToggleScrapVideo(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempAllowScrapVideo = !_tempAllowScrapVideo;
            UpdateScrapTargetsUI();
        }

        private async void OnConditionNoneTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.None;
            UpdateScrapConditionUI();
        }

        private async void OnConditionAllMissingTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.AllMediaMissing;
            UpdateScrapConditionUI();
        }

        private async void OnConditionLogoMissingTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.LogoMissing;
            UpdateScrapConditionUI();
        }

        private async void OnConditionCoverMissingTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.CoverMissing;
            UpdateScrapConditionUI();
        }

        private async void OnConditionVideoMissingTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempScrapCondition = ScrapCondition.VideoMissing;
            UpdateScrapConditionUI();
        }

        private async void OnToggleScrapTargets(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isScrapTargetsExpanded = !_isScrapTargetsExpanded;
            UpdateExpandersUI();
        }

        private async void OnToggleScrapCondition(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isScrapConditionExpanded = !_isScrapConditionExpanded;
            UpdateExpandersUI();
        }

        private async void OnToggleAdvanced(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isAdvancedExpanded = !_isAdvancedExpanded;
            UpdateExpandersUI();
        }

        private void OnDelayChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (DelayValueText != null)
                DelayValueText.Text = $"{(int)e.NewValue}ms";
        }

        private void OnHttpTimeoutChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (HttpTimeoutValueText != null)
                HttpTimeoutValueText.Text = $"{(int)e.NewValue}초";
        }

        private async void OnClearCacheTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;

            var result = await Services.DialogService.Instance.ShowConfirm(
                "캐시를 초기화하시겠습니까?",
                "모든 스크랩 캐시가 삭제되며, 다음 검색 시 API를 다시 호출합니다.");

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

        private async void OnResetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;

            var result = await Services.DialogService.Instance.ShowConfirm(
                "설정을 초기화하시겠습니까?",
                "모든 설정이 기본값으로 되돌아갑니다.");

            if (result)
            {
                await WavSounds.Confirm();
                ScreenScraperConfig.Instance.Reset();
                LoadSettings();
            }
        }

        private async void OnToggleAutoScrap(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempEnableAutoScrap = !_tempEnableAutoScrap;
            UpdateAutoScrapUI();
        }

        private async void OnToggleSearchMethod(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _isSearchMethodExpanded = !_isSearchMethodExpanded;
            UpdateExpandersUI();
        }

        private async void OnSearchByNameTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempSearchMethod = SearchMethod.ByFileName;
            UpdateSearchMethodUI();
        }

        private async void OnSearchByCrcTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            await WavSounds.Click();
            _tempSearchMethod = SearchMethod.ByCrc;
            UpdateSearchMethodUI();
        }

        private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true;
            SaveSettings();
        }
    }
}