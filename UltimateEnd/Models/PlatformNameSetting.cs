using ReactiveUI;
using System.Collections.Generic;
using System.IO;
using UltimateEnd.Enums;
using UltimateEnd.Services;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Models
{
    public class PlatformNameSetting : ViewModelBase
    {
        private string _basePath = string.Empty;
        private string _actualPath = string.Empty;
        private PlatformOption? _selectedPlatformOption;
        private string? _customDisplayName;

        public string BasePath
        {
            get => _basePath;
            set => this.RaiseAndSetIfChanged(ref _basePath, value);
        }

        public string FolderName { get; set; } = string.Empty;

        public string ActualPath
        {
            get => _actualPath;
            set => this.RaiseAndSetIfChanged(ref _actualPath, value);
        }

        public bool IsFolderMissing => _actualPath.StartsWith("[경로 없음]");

        public PlatformStatusType StatusType
        {
            get
            {
                if (IsFolderMissing) return PlatformStatusType.Missing;

                if (IsNew) return PlatformStatusType.New;

                return PlatformStatusType.Normal;
            }
        }

        public string StatusText
        {
            get
            {
                if (IsFolderMissing) return "폴더 없음";

                if (IsNew) return "새 폴더";

                return string.Empty;
            }
        }

        public PlatformOption? SelectedPlatformOption
        {
            get => _selectedPlatformOption;
            set => this.RaiseAndSetIfChanged(ref _selectedPlatformOption, value);
        }

        public string? SelectedPlatform => SelectedPlatformOption?.Id;

        public string? CustomDisplayName
        {
            get => _customDisplayName;
            set => this.RaiseAndSetIfChanged(ref _customDisplayName, value);
        }

        public bool IsNew { get; set; } = false;

        public List<PlatformOption> AvailablePlatforms { get; set; } = [];

        public string PlatformInfo => SelectedPlatform != null ? $"매핑: {PlatformInfoService.Instance.GetPlatformDisplayName(SelectedPlatform)}" : "플랫폼을 선택하세요";

        public string GetFullPath()
        {
            if (string.IsNullOrEmpty(BasePath)) return FolderName;

            return Path.Combine(BasePath, FolderName);
        }

        public string DisplayPath => string.IsNullOrEmpty(BasePath) ? ActualPath : $"{BasePath} → {ActualPath}";
    }
}