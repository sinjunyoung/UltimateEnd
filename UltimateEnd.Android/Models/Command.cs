using ReactiveUI;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using UltimateEnd.Android.Services;
using UltimateEnd.Android.Utils;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Models
{
    public class Command : ReactiveObject, IEmulatorCommand
    {
        private string? _coreName;
        private string _launchCommand = string.Empty;        
        bool _isRetroArch = false;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("isRetroArch")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsRetroArch
        {
            get => _isRetroArch;
            set => this.RaiseAndSetIfChanged(ref _isRetroArch, value);
        }

        [JsonPropertyName("supportedPlatforms")]
        public List<string> SupportedPlatforms { get; set; } = [];

        [JsonPropertyName("launchCommand")]
        public string LaunchCommand
        {
            get => _launchCommand;
            set => this.RaiseAndSetIfChanged(ref _launchCommand, value);
        }

        [JsonIgnore]
        public Avalonia.Media.Imaging.Bitmap Icon => new AppIconProvider().GetAppIcon(LaunchCommand)!;

        [JsonPropertyName("coreName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CoreName
        {
            get => _coreName;
            set => this.RaiseAndSetIfChanged(ref _coreName, value);
        }

        [JsonIgnore]
        public string Package => CommandLineParser.ExtractPackageName(LaunchCommand);

        [JsonIgnore]
        public string Activity
        {
            get
            {
                var component = CommandLineParser.ExtractComponentName(LaunchCommand);
                return component?.ClassName ?? string.Empty;
            }
        }
    }
}