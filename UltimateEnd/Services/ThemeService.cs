using Avalonia;
using Avalonia.Controls;
using System;
using System.Xml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimateEnd.Models;
using Avalonia.Media;

namespace UltimateEnd.Services
{
    public class ThemeService
    {
        private static string _currentThemeFileName = "DarkTheme";

        public static string CurrentThemeFileName => _currentThemeFileName;
        public static event Action<string>? ThemeChanged;

        private static string GetThemesFolder()
        {
            var provider = AppBaseFolderProviderFactory.Create?.Invoke();

            if (provider == null)
                return "Themes";

            var settingsFolder = provider.GetAppBaseFolder();
            var ultimateEndFolder = Directory.GetParent(settingsFolder)?.FullName;

            return ultimateEndFolder != null ? Path.Combine(ultimateEndFolder, "Themes") : "Themes";
        }

        public static void Initialize()
        {
            var savedTheme = LoadThemeFromSettings();
            ApplyTheme(savedTheme, saveToSettings: false);
        }

        public static void ApplyTheme(string themeFileName, bool saveToSettings = true)
        {
            var app = Application.Current;

            if (app == null) return;

            try
            {
                var oldTheme = app.Resources.MergedDictionaries
                    .OfType<ResourceDictionary>()
                    .FirstOrDefault();

                if (oldTheme != null)
                    app.Resources.MergedDictionaries.Remove(oldTheme);

                string themePath = Path.Combine(GetThemesFolder(), $"{themeFileName}.axaml");

                if (!File.Exists(themePath))
                {
                    int waitCount = 0;
                    while (!File.Exists(themePath) && waitCount < 20)
                    {
                        System.Threading.Thread.Sleep(100);
                        waitCount++;
                    }

                    if (!File.Exists(themePath))
                    {
                        if (themeFileName != "DarkTheme")
                        {
                            themePath = Path.Combine(GetThemesFolder(), "DarkTheme.axaml");
                            themeFileName = "DarkTheme";

                            if (!File.Exists(themePath))
                            {
                                ApplyFallbackTheme();
                                return;
                            }
                        }
                        else
                        {
                            ApplyFallbackTheme();
                            return;
                        }
                    }
                }

                string xamlContent = File.ReadAllText(themePath);

                using var stringReader = new StringReader(xamlContent);
                using var xmlReader = XmlReader.Create(stringReader);

                var themeDict = new ResourceDictionary();

                xmlReader.ReadToFollowing("ResourceDictionary");

                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType == XmlNodeType.Element)
                    {
                        if (xmlReader.Name == "SolidColorBrush")
                        {
                            var key = xmlReader.GetAttribute("x:Key");
                            var color = xmlReader.ReadElementContentAsString();

                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(color))
                                themeDict[key] = new SolidColorBrush(Color.Parse(color));
                        }
                        else if (xmlReader.Name == "LinearGradientBrush")
                        {
                            var key = xmlReader.GetAttribute("x:Key");
                            var startPoint = xmlReader.GetAttribute("StartPoint");
                            var endPoint = xmlReader.GetAttribute("EndPoint");

                            if (!string.IsNullOrEmpty(key))
                            {
                                var brush = new LinearGradientBrush();

                                if (!string.IsNullOrEmpty(startPoint))
                                    brush.StartPoint = RelativePoint.Parse(startPoint);
                                if (!string.IsNullOrEmpty(endPoint))
                                    brush.EndPoint = RelativePoint.Parse(endPoint);

                                using var subReader = xmlReader.ReadSubtree();

                                while (subReader.Read())
                                {
                                    if (subReader.NodeType == XmlNodeType.Element && subReader.Name == "GradientStop")
                                    {
                                        var stopColor = subReader.GetAttribute("Color");
                                        var stopOffset = subReader.GetAttribute("Offset");

                                        if (!string.IsNullOrEmpty(stopColor) && !string.IsNullOrEmpty(stopOffset))
                                        {
                                            brush.GradientStops.Add(new GradientStop
                                            {
                                                Color = Color.Parse(stopColor),
                                                Offset = double.Parse(stopOffset)
                                            });
                                        }
                                    }
                                }

                                themeDict[key] = brush;
                            }
                        }
                        else if (xmlReader.Name == "RadialGradientBrush")
                        {
                            var key = xmlReader.GetAttribute("x:Key");

                            if (!string.IsNullOrEmpty(key))
                            {
                                var brush = new RadialGradientBrush();

                                using var subReader = xmlReader.ReadSubtree();

                                while (subReader.Read())
                                {
                                    if (subReader.NodeType == XmlNodeType.Element && subReader.Name == "GradientStop")
                                    {
                                        var stopColor = subReader.GetAttribute("Color");
                                        var stopOffset = subReader.GetAttribute("Offset");

                                        if (!string.IsNullOrEmpty(stopColor) && !string.IsNullOrEmpty(stopOffset))
                                        {
                                            brush.GradientStops.Add(new GradientStop
                                            {
                                                Color = Color.Parse(stopColor),
                                                Offset = double.Parse(stopOffset)
                                            });
                                        }
                                    }
                                }

                                themeDict[key] = brush;
                            }
                        }
                        else if (xmlReader.Name == "Color")
                        {
                            var key = xmlReader.GetAttribute("x:Key");
                            var colorValue = xmlReader.ReadElementContentAsString();

                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(colorValue))
                                themeDict[key] = Color.Parse(colorValue);
                        }
                        else if (xmlReader.Name == "Thickness")
                        {
                            var key = xmlReader.GetAttribute("x:Key");
                            var thicknessValue = xmlReader.ReadElementContentAsString();

                            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(thicknessValue))
                                themeDict[key] = Thickness.Parse(thicknessValue);
                        }
                    }
                }

                app.Resources.MergedDictionaries.Add(themeDict);
                _currentThemeFileName = themeFileName;

                if (saveToSettings)
                    SaveThemeToSettings(themeFileName);

                ThemeChanged?.Invoke(themeFileName);
            }
            catch
            {
                ApplyFallbackTheme();
            }
        }

        private static void ApplyFallbackTheme()
        {
            var app = Application.Current;
            if (app == null) return;

            try
            {
                var themeDict = new ResourceDictionary();

                themeDict["Background.Primary"] = new SolidColorBrush(Color.Parse("#202020"));
                themeDict["Background.Secondary"] = new SolidColorBrush(Color.Parse("#2B2B2B"));
                themeDict["Background.Tertiary"] = new SolidColorBrush(Color.Parse("#1C1C1C"));
                themeDict["Background.Card"] = new SolidColorBrush(Color.Parse("#2B2B2B"));
                themeDict["Background.Input"] = new SolidColorBrush(Color.Parse("#323232"));
                themeDict["Background.Hover"] = new SolidColorBrush(Color.Parse("#3A3A3A"));
                themeDict["Background.Border"] = new SolidColorBrush(Color.Parse("#3F3F3F"));

                themeDict["Text.Primary"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                themeDict["Text.Secondary"] = new SolidColorBrush(Color.Parse("#E4E4E4"));
                themeDict["Text.Tertiary"] = new SolidColorBrush(Color.Parse("#A0A0A0"));
                themeDict["Text.Disabled"] = new SolidColorBrush(Color.Parse("#6D6D6D"));
                themeDict["Text.Muted"] = new SolidColorBrush(Color.Parse("#8A8A8A"));

                themeDict["Accent.Blue"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                themeDict["Accent.Red"] = new SolidColorBrush(Color.Parse("#D13438"));
                themeDict["Accent.Green"] = new SolidColorBrush(Color.Parse("#107C10"));
                themeDict["Accent.Yellow"] = new SolidColorBrush(Color.Parse("#FFB900"));

                themeDict["Accent.Blue.Transparent"] = new SolidColorBrush(Color.Parse("#40FFFFFF"));
                themeDict["Accent.Red.Transparent"] = new SolidColorBrush(Color.Parse("#40D13438"));

                var gradientPrimary = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
                };
                gradientPrimary.GradientStops.Add(new GradientStop { Color = Color.Parse("#FFFFFF"), Offset = 0 });
                gradientPrimary.GradientStops.Add(new GradientStop { Color = Color.Parse("#CCCCCC"), Offset = 1 });
                themeDict["Gradient.Primary"] = gradientPrimary;

                var gradientBackground = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
                };
                gradientBackground.GradientStops.Add(new GradientStop { Color = Color.Parse("#1C1C1C"), Offset = 0.0 });
                gradientBackground.GradientStops.Add(new GradientStop { Color = Color.Parse("#202020"), Offset = 0.5 });
                gradientBackground.GradientStops.Add(new GradientStop { Color = Color.Parse("#2B2B2B"), Offset = 1.0 });
                themeDict["Gradient.Background"] = gradientBackground;

                var glowBlue = new RadialGradientBrush();
                glowBlue.GradientStops.Add(new GradientStop { Color = Color.Parse("#0078D4"), Offset = 0 });
                glowBlue.GradientStops.Add(new GradientStop { Color = Colors.Transparent, Offset = 1 });
                themeDict["Gradient.Glow.Blue"] = glowBlue;

                var glowRed = new RadialGradientBrush();
                glowRed.GradientStops.Add(new GradientStop { Color = Color.Parse("#D13438"), Offset = 0 });
                glowRed.GradientStops.Add(new GradientStop { Color = Colors.Transparent, Offset = 1 });
                themeDict["Gradient.Glow.Red"] = glowRed;

                themeDict["TextBox.Background"] = new SolidColorBrush(Color.Parse("#323232"));
                themeDict["TextBox.Foreground"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                themeDict["TextBox.BorderBrush"] = new SolidColorBrush(Color.Parse("#5A5A5A"));
                themeDict["TextBox.CaretBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                themeDict["TextBox.SelectionBrush"] = new SolidColorBrush(Color.Parse("#404040"));
                themeDict["TextBox.SelectionForegroundBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));

                themeDict["Description.Foreground"] = new SolidColorBrush(Color.Parse("#A0A0A0"));
                themeDict["Description.PlaceholderForeground"] = new SolidColorBrush(Color.Parse("#6D6D6D"));

                themeDict["Button.Primary.Background"] = new SolidColorBrush(Color.Parse("#0078D4"));
                themeDict["Button.Danger.Background"] = new SolidColorBrush(Color.Parse("#D13438"));
                themeDict["Button.Foreground"] = new SolidColorBrush(Color.Parse("#000000"));
                themeDict["Button.Hover.Foreground"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                themeDict["Button.Hover.Background"] = new SolidColorBrush(Color.Parse("#3A3A3A"));

                themeDict["ComboBox.Background"] = new SolidColorBrush(Color.Parse("#323232"));
                themeDict["ComboBox.Foreground"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                themeDict["ComboBox.Border"] = new SolidColorBrush(Color.Parse("#5A5A5A"));
                themeDict["ComboBox.Item.Background"] = new SolidColorBrush(Color.Parse("#2B2B2B"));
                themeDict["ComboBox.Item.Hover"] = new SolidColorBrush(Color.Parse("#3A3A3A"));

                themeDict["CheckBox.Foreground"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                themeDict["CheckBox.Box.BorderBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));

                themeDict["Shadow.Color"] = Color.Parse("#000000");
                themeDict["Logo.Shadow.Color"] = Color.Parse("#FFFFFF");
                themeDict["Glow.Blue"] = Color.Parse("#0078D4");
                themeDict["Glow.Red"] = Color.Parse("#D13438");

                themeDict["Focus.BorderBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));
                themeDict["Focus.BorderThickness"] = new Thickness(2);

                themeDict["Selection.BorderBrush"] = new SolidColorBrush(Color.Parse("#FFFFFF"));

                themeDict["Toggle.Background"] = new SolidColorBrush(Color.Parse("#CCCCCC"));
                themeDict["Toggle.SelectionBackground"] = new SolidColorBrush(Color.Parse("#4CAF50"));

                app.Resources.MergedDictionaries.Add(themeDict);
                _currentThemeFileName = "DefaultTheme";

                ThemeChanged?.Invoke("DefaultTheme");
            }
            catch { }
        }

        public static List<ThemeOption> GetAvailableThemes()
        {
            var themes = new List<ThemeOption>();
            string themesFolder = GetThemesFolder();

            try
            {
                if (!Directory.Exists(themesFolder))
                {
                    Directory.CreateDirectory(themesFolder);
                    return themes;
                }

                var themeFiles = Directory.GetFiles(themesFolder, "*.axaml");

                foreach (var filePath in themeFiles)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    var colors = ExtractPreviewColors(filePath);

                    themes.Add(new ThemeOption
                    {
                        Name = fileName,
                        PreviewColor1 = colors.Item1,
                        PreviewColor2 = colors.Item2,
                        PreviewColor3 = colors.Item3,
                        IsSelected = fileName == _currentThemeFileName
                    });
                }
            }
            catch { }

            return themes;
        }

        private static (string, string, string) ExtractPreviewColors(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);

                string color1 = ExtractColorFromKey(content, "Background.Primary") ?? "#202020";
                string color2 = ExtractColorFromKey(content, "Accent.Blue") ?? "#FFFFFF";
                string color3 = ExtractColorFromKey(content, "Text.Primary") ?? "#FFFFFF";

                return (color1, color2, color3);
            }
            catch
            {
                return ("#202020", "#FFFFFF", "#FFFFFF");
            }
        }

        private static string? ExtractColorFromKey(string content, string key)
        {
            try
            {
                var keyPattern = $"x:Key=\"{key}\">";
                int keyIndex = content.IndexOf(keyPattern);

                if (keyIndex == -1) return null;

                int startIndex = keyIndex + keyPattern.Length;
                int endIndex = content.IndexOf('<', startIndex);

                if (endIndex == -1) return null;

                return content[startIndex..endIndex].Trim();
            }
            catch
            {
                return null;
            }
        }

        public static string GetCurrentThemeName() => _currentThemeFileName;

        private static string LoadThemeFromSettings()
        {
            try
            {
                var settings = SettingsService.LoadSettings();

                if (!string.IsNullOrEmpty(settings.Theme))
                    return settings.Theme;

                return "DarkTheme";
            }
            catch
            {
                return "DarkTheme";
            }
        }

        private static void SaveThemeToSettings(string themeFileName)
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                settings.Theme = themeFileName;
                SettingsService.SaveThemeSettings(settings);
            }
            catch { }
        }
    }
}