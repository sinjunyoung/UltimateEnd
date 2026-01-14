using ReactiveUI;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using UltimateEnd.Desktop.Services;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Models
{
    public class Command : ReactiveObject, IEmulatorCommand
    {
        private string? _coreName;
        private string _launchCommand = string.Empty;
        private string? _workingDirectory;
        private string? _prelaunchScript;
        private string? _postlaunchScript;
        private bool _isRetroArch;

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

        [JsonPropertyName("workingDirectory")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? WorkingDirectory
        {
            get => _workingDirectory;
            set => this.RaiseAndSetIfChanged(ref _workingDirectory, value);
        }

        [JsonPropertyName("coreName")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CoreName
        {
            get => _coreName;
            set => this.RaiseAndSetIfChanged(ref _coreName, value);
        }

        [JsonPropertyName("prelaunchScript")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PrelaunchScript
        {
            get => _prelaunchScript;
            set => this.RaiseAndSetIfChanged(ref _prelaunchScript, value);
        }

        [JsonPropertyName("postlaunchScript")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PostlaunchScript
        {
            get => _postlaunchScript;
            set => this.RaiseAndSetIfChanged(ref _postlaunchScript, value);
        }

        [JsonIgnore]
        public string Executable
        {
            get => Utils.CommandParser.ExtractExecutable(LaunchCommand);
            set
            {
                var (_, args) = Utils.CommandParser.ParseCommand(LaunchCommand);
                LaunchCommand = string.IsNullOrEmpty(args)
                    ? value
                    : (value.Contains(' ') ? $"\"{value}\" {args}" : $"{value} {args}");
                this.RaisePropertyChanged(nameof(Executable));
                this.RaisePropertyChanged(nameof(Arguments));
                this.RaisePropertyChanged(nameof(Icon));
            }
        }

        [JsonIgnore]
        public string Arguments
        {
            get
            {
                var (_, args) = Utils.CommandParser.ParseCommand(LaunchCommand);
                return args;
            }
            set
            {
                var exec = Executable;
                LaunchCommand = string.IsNullOrEmpty(value)
                    ? exec
                    : (exec.Contains(' ') ? $"\"{exec}\" {value}" : $"{exec} {value}");
            }
        }
    }
}