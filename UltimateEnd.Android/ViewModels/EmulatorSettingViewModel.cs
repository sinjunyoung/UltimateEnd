using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Input;
using UltimateEnd.Android.Models;
using UltimateEnd.Android.Services;
using UltimateEnd.Android.Utils;
using UltimateEnd.Services;
using UltimateEnd.Utils;

namespace UltimateEnd.Android.ViewModels
{
    public class EmulatorSettingViewModel : UltimateEnd.ViewModels.EmulatorSettingViewModelBase, IDisposable
    {
        #region Fields

        private readonly InstalledAppsService _appsService;
        private string _packageName = string.Empty;
        private string _activityName = string.Empty;
        private string _selectedAction = "android.intent.action.VIEW";
        private string _selectedCategory = string.Empty;
        private string _dataUri = "{romPath}";
        private bool _flagNewTask = true;
        private bool _flagClearTop = false;
        private bool _flagSingleTop = false;
        private bool _flagClearTask = false;
        private bool _flagNoHistory = false;
        private string _previewLaunchCommand = string.Empty;
        private bool _isLoading = false;
        private ObservableCollection<ActivityInfo> _availableActivities = [];
        private ActivityInfo? _selectedActivity;
        private Bitmap? _selectedAppIcon;
        private string _selectedAppName = string.Empty;
        private readonly CompositeDisposable _disposables = [];

        #endregion

        #region Properties

        public ObservableCollection<IntentExtra> Extras { get; set; }
        public List<string> AvailableActions { get; }
        public List<string> AvailableCategories { get; }
        public List<string> ExtraTypes { get; }

        public string PackageName
        {
            get => _packageName;
            set
            {
                this.RaiseAndSetIfChanged(ref _packageName, value);
                UpdatePreview();
            }
        }

        public string ActivityName
        {
            get => _activityName;
            set
            {
                this.RaiseAndSetIfChanged(ref _activityName, value);
                UpdatePreview();
            }
        }

        public string SelectedAction
        {
            get => _selectedAction;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedAction, value);
                UpdatePreview();
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCategory, value);
                UpdatePreview();
            }
        }

        public string DataUri
        {
            get => _dataUri;
            set
            {
                this.RaiseAndSetIfChanged(ref _dataUri, value);
                UpdatePreview();
            }
        }

        public bool FlagNewTask
        {
            get => _flagNewTask;
            set
            {
                this.RaiseAndSetIfChanged(ref _flagNewTask, value);
                UpdatePreview();
            }
        }

        public bool FlagClearTop
        {
            get => _flagClearTop;
            set
            {
                this.RaiseAndSetIfChanged(ref _flagClearTop, value);
                UpdatePreview();
            }
        }

        public bool FlagSingleTop
        {
            get => _flagSingleTop;
            set
            {
                this.RaiseAndSetIfChanged(ref _flagSingleTop, value);
                UpdatePreview();
            }
        }

        public bool FlagClearTask
        {
            get => _flagClearTask;
            set
            {
                this.RaiseAndSetIfChanged(ref _flagClearTask, value);
                UpdatePreview();
            }
        }

        public bool FlagNoHistory
        {
            get => _flagNoHistory;
            set
            {
                this.RaiseAndSetIfChanged(ref _flagNoHistory, value);
                UpdatePreview();
            }
        }

        public string PreviewLaunchCommand
        {
            get => _previewLaunchCommand;
            private set => this.RaiseAndSetIfChanged(ref _previewLaunchCommand, value);
        }

        public ObservableCollection<ActivityInfo> AvailableActivities
        {
            get => _availableActivities;
            set => this.RaiseAndSetIfChanged(ref _availableActivities, value);
        }

        public ActivityInfo? SelectedActivity
        {
            get => _selectedActivity;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedActivity, value);
                if (value != null)
                {
                    ActivityName = value.Name;

                    if (value.SupportsView && SelectedAction != "android.intent.action.VIEW")
                        SelectedAction = "android.intent.action.VIEW";
                }
            }
        }

        public Avalonia.Media.Imaging.Bitmap? SelectedAppIcon
        {
            get => _selectedAppIcon;
            set => this.RaiseAndSetIfChanged(ref _selectedAppIcon, value);
        }

        public string SelectedAppName
        {
            get => _selectedAppName;
            set => this.RaiseAndSetIfChanged(ref _selectedAppName, value);
        }

        public ICommand GoBackCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand AddExtraCommand { get; }
        public ICommand RemoveExtraCommand { get; }

        #endregion

        #region Constructor

        public EmulatorSettingViewModel()
        {
            _appsService = new InstalledAppsService();
            Extras = [];

            AvailableActions =
            [
                "android.intent.action.VIEW",
                "android.intent.action.MAIN"
            ];

            AvailableCategories =
            [
                string.Empty,
                "android.intent.category.LAUNCHER",
                "android.intent.category.DEFAULT",
                "android.intent.category.BROWSABLE"
            ];

            ExtraTypes =
            [
                "string",
                "int",
                "boolean",
                "long",
                "float",
                "double"
            ];

            GoBackCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                GoBack();
                await WavSounds.Cancel();
            });

            ClearSearchCommand = ReactiveCommand.Create(() => SearchText = string.Empty);
            AddExtraCommand = ReactiveCommand.Create(AddExtra);
            RemoveExtraCommand = ReactiveCommand.Create<IntentExtra>(RemoveExtra);

            Extras.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (IntentExtra extra in e.NewItems)
                    {
                        extra.WhenAnyValue(x => x.Type, x => x.Key, x => x.Value)
                            .Subscribe(_ => UpdatePreview())
                            .DisposeWith(_disposables);
                    }
                }
            };

            this.WhenAnyValue(x => x.SelectedCommand)
                .Where(x => x is Command)
                .Cast<Command>()
                .Subscribe(cmd =>
                {
                    LoadCommandToUI(cmd);

                    cmd.WhenAnyValue(x => x.CoreName)
                        .Skip(1)
                        .Where(_ => cmd.IsRetroArch)
                        .Subscribe(coreName => UpdateLibretroExtra(coreName))
                        .DisposeWith(_disposables);
                })
                .DisposeWith(_disposables);
        }

        #endregion

        #region Public Methods

        public void SetSelectedApp(InstalledAppInfo appInfo)
        {
            if (appInfo == null) return;

            PackageName = appInfo.PackageName;
            SelectedAppIcon = appInfo.Icon;
            SelectedAppName = appInfo.DisplayName;

            LoadActivitiesForPackage(appInfo.PackageName, true);
        }

        public override Bitmap LoadPlatformImage(string platformId)
        {
            try
            {
                var uri = new Uri(ResourceHelper.GetPlatformImage(platformId));
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    return new Bitmap(stream);
                }
            }
            catch { }

            return null!;
        }

        public void Dispose() => _disposables?.Dispose();

        #endregion

        #region Protected Override Methods

        protected override ICommandConfigService GetConfigService() => new CommandConfigService();

        protected override IEmulatorCommand CreateNewCommand()
        {
            return new Command
            {
                Id = Guid.NewGuid().ToString(),
                Name = "새 커맨드",
                IsRetroArch = false,
                SupportedPlatforms = [],
                LaunchCommand = "am start -n com.example.app --activity-clear-task --activity-clear-top --activity-no-history"
            };
        }

        protected override IEmulatorCommand CreateDuplicateCommand(IEmulatorCommand original)
        {
            return new Command
            {
                Id = Guid.NewGuid().ToString(),
                Name = original.Name + " (복사)",
                IsRetroArch = original.IsRetroArch,
                CoreName = original.CoreName,
                SupportedPlatforms = [.. original.SupportedPlatforms],
                LaunchCommand = original.LaunchCommand
            };
        }

        protected override void OnPlatformsChanged(List<string> platforms)
        {
            if (SelectedCommand is Command cmd)
                cmd.SupportedPlatforms = platforms;
        }

        #endregion

        #region Private Methods - Activity & App Info Loading

        private void LoadActivitiesForPackage(string packageName, bool autoSelect = false)
        {
            try
            {
                AvailableActivities.Clear();

                if (autoSelect)
                {
                    var (activities, selected) = _appsService.GetPackageActivitiesWithAutoSelect(packageName);

                    foreach (var activity in activities)
                        AvailableActivities.Add(activity);

                    if (selected != null)
                        SelectedActivity = selected;
                }
                else
                {
                    var activities = _appsService.GetPackageActivities(packageName);

                    foreach (var activity in activities)
                        AvailableActivities.Add(activity);
                }
            }
            catch
            {
                AvailableActivities.Clear();
            }
        }

        private void LoadAppIconAndName(string packageName)
        {
            var (icon, appName) = _appsService.GetAppIconAndName(packageName);
            SelectedAppIcon = icon;
            SelectedAppName = appName;
        }

        #endregion

        #region Private Methods - Command Loading

        private void LoadCommandToUI(IEmulatorCommand command)
        {
            if (command is not Command androidCommand) return;

            try
            {
                _isLoading = true;

                var extractedPackageName = CommandLineParser.ExtractPackageName(androidCommand.LaunchCommand) ?? string.Empty;
                PackageName = extractedPackageName;

                var componentName = CommandLineParser.ExtractComponentName(androidCommand.LaunchCommand);
                var extractedActivityName = componentName?.ClassName ?? string.Empty;
                ActivityName = extractedActivityName;

                LoadAppIconAndName(extractedPackageName);

                if (!string.IsNullOrEmpty(extractedPackageName))
                {
                    LoadActivitiesForPackage(extractedPackageName, autoSelect: false);

                    if (!string.IsNullOrEmpty(extractedActivityName))
                    {
                        var matchingActivity = AvailableActivities.FirstOrDefault(a => a.Name == extractedActivityName);
                        if (matchingActivity != null)
                        {
                            _selectedActivity = matchingActivity;
                            this.RaisePropertyChanged(nameof(SelectedActivity));
                        }
                    }
                }
                else
                {
                    SelectedAppIcon = null;
                    SelectedAppName = string.Empty;
                    AvailableActivities.Clear();
                    SelectedActivity = null;
                }

                ParseLaunchCommand(androidCommand.LaunchCommand);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void UpdateLibretroExtra(string coreName)
        {
            if (string.IsNullOrEmpty(coreName) || SelectedCommand is not Command cmd || !cmd.IsRetroArch)
                return;

            var libretroExtra = Extras.FirstOrDefault(e =>
                e.Key.Equals("LIBRETRO", StringComparison.OrdinalIgnoreCase));

            if (libretroExtra != null)
            {
                var currentPath = libretroExtra.Value;
                var match = System.Text.RegularExpressions.Regex.Match(
                    currentPath,
                    @"^(.+/cores/)[^/]+(_libretro_android\.so)$");

                if (match.Success)
                    libretroExtra.Value = $"{match.Groups[1].Value}{coreName}{match.Groups[2].Value}";
                else if (!string.IsNullOrEmpty(PackageName))
                    libretroExtra.Value = $"/data/data/{PackageName}/cores/{coreName}_libretro_android.so";
            }
            else if (!string.IsNullOrEmpty(PackageName))
            {
                Extras.Add(new IntentExtra
                {
                    Type = "string",
                    Key = "LIBRETRO",
                    Value = $"/data/data/{PackageName}/cores/{coreName}_libretro_android.so"
                });
            }
        }

        #endregion

        #region Private Methods - Command Parsing

        private void ParseLaunchCommand(string launchCommand)
        {
            Extras.Clear();

            if (string.IsNullOrWhiteSpace(launchCommand))
            {
                UpdatePreview();
                return;
            }

            var args = CommandLineParser.SplitCommandLine(launchCommand);

            FlagNewTask = false;
            FlagClearTop = false;
            FlagSingleTop = false;
            FlagClearTask = false;
            FlagNoHistory = false;
            SelectedAction = "android.intent.action.VIEW";
            SelectedCategory = string.Empty;
            DataUri = string.Empty;

            var unsupportedArgs = new List<string>();

            int i = 0;
            while (i < args.Count)
            {
                var arg = args[i++];

                switch (arg.ToLowerInvariant())
                {
                    case "-a":
                        if (i < args.Count) SelectedAction = args[i++];
                        break;
                    case "-c":
                        if (i < args.Count) SelectedCategory = args[i++];
                        break;
                    case "-d":
                        if (i < args.Count) DataUri = args[i++];
                        break;
                    case "-t":
                    case "-i":
                    case "-p":
                    case "-f":
                        unsupportedArgs.Add(arg);
                        if (i < args.Count) unsupportedArgs.Add(args[i++]);
                        break;
                    case "-n":
                        if (i < args.Count) i++;
                        break;

                    case "-e":
                    case "--es":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "string", Key = args[i++], Value = args[i++] });
                        break;
                    case "--esn":
                        if (i < args.Count)
                            Extras.Add(new IntentExtra { Type = "string", Key = args[i++], Value = "null" });
                        break;

                    case "--ei":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "int", Key = args[i++], Value = args[i++] });
                        break;
                    case "--eia":
                    case "--eial":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "int", Key = args[i++], Value = args[i++] });
                        break;

                    case "--el":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "long", Key = args[i++], Value = args[i++] });
                        break;
                    case "--ela":
                    case "--elal":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "long", Key = args[i++], Value = args[i++] });
                        break;

                    case "--ef":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "float", Key = args[i++], Value = args[i++] });
                        break;
                    case "--efa":
                    case "--efal":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "float", Key = args[i++], Value = args[i++] });
                        break;

                    case "--ed":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "double", Key = args[i++], Value = args[i++] });
                        break;

                    case "--ez":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "boolean", Key = args[i++], Value = args[i++] });
                        break;

                    case "--esa":
                    case "--esal":
                    case "--eu":
                    case "--ecn":
                        if (i + 1 < args.Count)
                            Extras.Add(new IntentExtra { Type = "string", Key = args[i++], Value = args[i++] });
                        break;

                    case "--grant-read-uri-permission":
                    case "--grant-write-uri-permission":
                    case "--grant-persistable-uri-permission":
                    case "--grant-prefix-uri-permission":
                    case "--activity-brought-to-front":
                    case "--activity-clear-when-task-reset":
                    case "--activity-exclude-from-recents":
                    case "--activity-launched-from-history":
                    case "--activity-multiple-task":
                    case "--activity-no-animation":
                    case "--activity-no-user-action":
                    case "--activity-previous-is-top":
                    case "--activity-reorder-to-front":
                    case "--activity-reset-task-if-needed":
                    case "--activity-task-on-home":
                    case "--activity-match-external":
                    case "--exclude-stopped-packages":
                    case "--include-stopped-packages":
                    case "--debug-log-resolution":
                    case "--receiver-registered-only":
                    case "--receiver-replace-pending":
                    case "--receiver-foreground":
                    case "--receiver-no-abort":
                    case "-D":
                    case "-N":
                    case "-W":
                    case "-S":
                    case "--streaming":
                    case "--track-allocation":
                    case "--task-overlay":
                    case "--lock-task":
                    case "--allow-background-activity-starts":
                        break;

                    case "--activity-clear-task":
                        FlagClearTask = true;
                        break;
                    case "--activity-clear-top":
                        FlagClearTop = true;
                        break;
                    case "--activity-no-history":
                        FlagNoHistory = true;
                        break;
                    case "--activity-single-top":
                        FlagSingleTop = true;
                        break;
                    case "--activity-new-task":
                        FlagNewTask = true;
                        break;

                    case "-P":
                    case "-R":
                    case "--start-profiler":
                    case "--sampling":
                    case "--attach-agent":
                    case "--attach-agent-bind":
                    case "--user":
                    case "--receiver-permission":
                    case "--display":
                    case "--windowingmode":
                    case "--activitytype":
                    case "--task":
                        unsupportedArgs.Add(arg);
                        if (i < args.Count) unsupportedArgs.Add(args[i++]);
                        break;

                    default:
                        if (arg.StartsWith('-') || arg.StartsWith("--"))
                            unsupportedArgs.Add(arg);
                        break;
                }
            }

            if (unsupportedArgs.Count > 0)
            {
                Extras.Add(new IntentExtra
                {
                    Type = "string",
                    Key = "__UNSUPPORTED_ARGS__",
                    Value = string.Join(" ", unsupportedArgs.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))
                });
            }

            UpdatePreview();
        }

        #endregion

        #region Private Methods - Command Update
        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(PackageName))
            {
                PreviewLaunchCommand = string.Empty;
                if (!_isLoading)
                    SaveToCommand();
                return;
            }

            var sb = new StringBuilder("am start");

            if (!string.IsNullOrEmpty(ActivityName))
                sb.AppendLine($"\n  -n {PackageName}/{ActivityName}");
            else
                sb.AppendLine($"\n  -n {PackageName}");

            if (!string.IsNullOrEmpty(SelectedAction))
                sb.AppendLine($"  -a {SelectedAction}");

            if (!string.IsNullOrEmpty(SelectedCategory))
                sb.AppendLine($"  -c {SelectedCategory}");

            if (!string.IsNullOrEmpty(DataUri))
                sb.AppendLine($"  -d {DataUri}");

            foreach (var extra in Extras)
            {
                if (string.IsNullOrEmpty(extra.Key)) continue;

                if (extra.Key == "__UNSUPPORTED_ARGS__")
                {
                    sb.AppendLine($"  {extra.Value}");
                    continue;
                }

                var flag = extra.Type switch
                {
                    "string" => "--es",
                    "int" => "--ei",
                    "boolean" => "--ez",
                    "long" => "--el",
                    "float" => "--ef",
                    "double" => "--ed",
                    _ => "--es"
                };
                sb.AppendLine($"  {flag} {extra.Key} {extra.Value}");
            }

            if (FlagClearTask) sb.AppendLine("  --activity-clear-task");
            if (FlagClearTop) sb.AppendLine("  --activity-clear-top");
            if (FlagNoHistory) sb.AppendLine("  --activity-no-history");
            if (FlagSingleTop) sb.AppendLine("  --activity-single-top");
            if (FlagNewTask) sb.AppendLine("  --activity-new-task");

            PreviewLaunchCommand = sb.ToString().TrimEnd();

            if (!_isLoading)
                SaveToCommand();
        }

        private void SaveToCommand()
        {
            if (SelectedCommand is Command cmd)
                cmd.LaunchCommand = PreviewLaunchCommand;
        }

        #endregion

        #region Private Methods - Extra Management

        private void AddExtra() => Extras.Add(new IntentExtra { Type = "string", Key = string.Empty, Value = string.Empty });

        private void RemoveExtra(IntentExtra extra) => Extras.Remove(extra);

        #endregion
    }
}