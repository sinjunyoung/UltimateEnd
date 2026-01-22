using System;
using System.Collections.ObjectModel;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        public ObservableCollection<ThemeOption> AvailableThemes { get; } = [];
        public event Action? BackRequested;

        public SettingsViewModel()
        {
            LoadThemes();
            ThemeService.ThemeChanged += OnThemeChanged;
        }

        private void LoadThemes()
        {
            AvailableThemes.Clear();

            var themes = ThemeService.GetAvailableThemes();
            foreach (var theme in themes)
            {
                AvailableThemes.Add(theme);
            }
        }

        private void OnThemeChanged(string themeFileName)
        {
            foreach (var themeOption in AvailableThemes)
                themeOption.IsSelected = themeOption.Name == themeFileName;
        }

        public void SelectTheme(ThemeOption theme)
        {
            if (theme.Name != ThemeService.CurrentThemeFileName)
                ThemeService.ApplyTheme(theme.Name);
        }

        public void RequestBack() => BackRequested?.Invoke();

        public void Dispose() => ThemeService.ThemeChanged -= OnThemeChanged;
    }
}