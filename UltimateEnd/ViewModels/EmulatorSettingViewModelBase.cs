using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using ReactiveUI;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.ViewModels
{
    public abstract class EmulatorSettingViewModelBase : ViewModelBase
    {
        private IEmulatorCommand _selectedCommand;
        private string _searchText = string.Empty;
        private PlatformInfo _selectedPlatformToAdd;
        private PlatformInfo _filterPlatform;

        public ObservableCollection<IEmulatorCommand> Commands { get; set; }
        public ObservableCollection<IEmulatorCommand> FilteredCommands { get; set; }
        public ObservableCollection<PlatformTag> SelectedPlatforms { get; set; }
        public ObservableCollection<PlatformInfo> AvailablePlatforms { get; set; }
        public ObservableCollection<PlatformInfo> FilterPlatforms { get; set; }

        public event Action? BackRequested;

        public IEmulatorCommand SelectedCommand
        {
            get => _selectedCommand;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedCommand, value);
                if (value != null)
                {
                    LoadPlatformsForCommand(value);
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                this.RaiseAndSetIfChanged(ref _searchText, value);
                FilterCommands();
            }
        }

        public PlatformInfo SelectedPlatformToAdd
        {
            get => _selectedPlatformToAdd;
            set => this.RaiseAndSetIfChanged(ref _selectedPlatformToAdd, value);
        }

        public PlatformInfo FilterPlatform
        {
            get => _filterPlatform;
            set
            {
                this.RaiseAndSetIfChanged(ref _filterPlatform, value);
                FilterCommands();
            }
        }

        public ICommand AddCommandCommand { get; }
        public ICommand DuplicateCommandCommand { get; }
        public ICommand DeleteCommandCommand { get; }
        public ICommand SaveCommandCommand { get; }
        public ICommand AddPlatformCommand { get; }
        public ICommand RemovePlatformCommand { get; }

        protected EmulatorSettingViewModelBase()
        {
            Commands = new ObservableCollection<IEmulatorCommand>();
            FilteredCommands = new ObservableCollection<IEmulatorCommand>();
            SelectedPlatforms = new ObservableCollection<PlatformTag>();
            AvailablePlatforms = new ObservableCollection<PlatformInfo>();
            FilterPlatforms = new ObservableCollection<PlatformInfo>();

            LoadAvailablePlatforms();
            LoadCommands();

            AddCommandCommand = ReactiveCommand.Create(AddCommand);
            DuplicateCommandCommand = ReactiveCommand.Create(DuplicateCommand,
                this.WhenAnyValue(x => x.SelectedCommand).Select(c => c != null));
            DeleteCommandCommand = ReactiveCommand.Create(DeleteCommand,
                this.WhenAnyValue(x => x.SelectedCommand).Select(c => c != null));
            SaveCommandCommand = ReactiveCommand.Create(SaveCommand,
                this.WhenAnyValue(x => x.SelectedCommand).Select(c => c != null));
            AddPlatformCommand = ReactiveCommand.Create(AddPlatform);
            RemovePlatformCommand = ReactiveCommand.Create<PlatformTag>(RemovePlatform);
        }

        protected abstract ICommandConfigService GetConfigService();
        protected abstract IEmulatorCommand CreateNewCommand();
        protected virtual IEmulatorCommand CreateDuplicateCommand(IEmulatorCommand original)
        {
            return CreateNewCommand();
        }
        protected abstract void OnPlatformsChanged(List<string> platforms);

        private void LoadAvailablePlatforms()
        {
            try
            {
                var database = PlatformInfoService.LoadDatabase();

                FilterPlatforms.Add(new PlatformInfo
                {
                    Id = null,
                    DisplayName = "전체",
                    Image = null
                });

                foreach (var platform in database.Platforms.OrderBy(p => p.DisplayName))
                {
                    var platformInfo = new PlatformInfo
                    {
                        Id = platform.Id,
                        DisplayName = platform.DisplayName,
                        Image = LoadPlatformImage(platform.Id)
                    };

                    AvailablePlatforms.Add(platformInfo);
                    FilterPlatforms.Add(platformInfo);
                }

                FilterPlatform = FilterPlatforms[0];
            }
            catch { }
        }

        public abstract Bitmap LoadPlatformImage(string platformId);

        private void LoadCommands()
        {
            try
            {
                Commands.Clear();

                var configService = GetConfigService();
                var config = configService.LoadConfig();

                foreach (var emulator in config.EmulatorCommands.Values)
                    Commands.Add(emulator);

                FilterCommands();
            }
            catch { }
        }

        private void FilterCommands()
        {
            FilteredCommands.Clear();

            var filtered = Commands.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(c =>
                    c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    c.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (FilterPlatform != null && !string.IsNullOrEmpty(FilterPlatform.Id))
            {
                filtered = filtered.Where(c =>
                    c.SupportedPlatforms.Any(p =>
                        IsPlatformMatch(p, FilterPlatform.Id)));
            }

            foreach (var cmd in filtered)
                FilteredCommands.Add(cmd);
        }

        private bool IsPlatformMatch(string commandPlatform, string filterPlatformId)
        {
            if (commandPlatform.Equals(filterPlatformId, StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                var database = PlatformInfoService.LoadDatabase();
                var platform = database.Platforms.FirstOrDefault(p => p.Id == filterPlatformId);

                if (platform?.Aliases != null)
                {
                    return platform.Aliases.Any(a =>
                        a.Equals(commandPlatform, StringComparison.OrdinalIgnoreCase));
                }
            }
            catch { }

            return false;
        }

        private void LoadPlatformsForCommand(IEmulatorCommand command)
        {
            SelectedPlatforms.Clear();

            foreach (var platformId in command.SupportedPlatforms)
            {
                SelectedPlatforms.Add(new PlatformTag
                {
                    Id = platformId,
                    Image = LoadPlatformImage(GetFullPlatformId(platformId))
                });
            }
        }

        private string GetFullPlatformId(string alias)
        {
            try
            {
                var database = PlatformInfoService.LoadDatabase();
                var platform = database.Platforms.FirstOrDefault(p =>
                    p.Id.Equals(alias, StringComparison.OrdinalIgnoreCase) ||
                    (p.Aliases != null && p.Aliases.Any(a => a.Equals(alias, StringComparison.OrdinalIgnoreCase))));

                return platform?.Id ?? alias;
            }
            catch
            {
                return alias;
            }
        }

        private void AddPlatform()
        {
            if (SelectedPlatformToAdd == null) return;

            var platformId = GetShortestAlias(SelectedPlatformToAdd.Id);

            if (SelectedPlatforms.Any(p => p.Id == platformId)) return;

            SelectedPlatforms.Add(new PlatformTag
            {
                Id = platformId,
                Image = SelectedPlatformToAdd.Image
            });
            UpdateCommandPlatforms();
        }

        public string GetShortestAlias(string platformId)
        {
            try
            {
                var database = PlatformInfoService.LoadDatabase();
                var platform = database.Platforms.FirstOrDefault(p => p.Id == platformId);

                if (platform?.Aliases != null && platform.Aliases.Count > 0)
                    return platform.Aliases.OrderBy(a => a.Length).First();
            }
            catch { }

            return platformId;
        }

        private void RemovePlatform(PlatformTag tag)
        {
            SelectedPlatforms.Remove(tag);
            UpdateCommandPlatforms();
        }

        private void UpdateCommandPlatforms()
        {
            if (SelectedCommand == null) return;

            OnPlatformsChanged(SelectedPlatforms.Select(p => p.Id).ToList());
        }

        private void AddCommand()
        {
            var newCommand = CreateNewCommand();
            Commands.Add(newCommand);
            FilterCommands();
            SelectedCommand = newCommand;
        }

        private void DuplicateCommand()
        {
            if (SelectedCommand == null) return;

            var duplicated = CreateDuplicateCommand(SelectedCommand);
            Commands.Add(duplicated);
            FilterCommands();
            SelectedCommand = duplicated;
        }

        private void DeleteCommand()
        {
            if (SelectedCommand == null) return;

            var configService = GetConfigService();
            var config = configService.LoadConfig();

            if (config.EmulatorCommands.ContainsKey(SelectedCommand.Id))
            {
                config.EmulatorCommands.Remove(SelectedCommand.Id);
                configService.SaveConfig(config);
            }

            Commands.Remove(SelectedCommand);
            FilterCommands();
            SelectedCommand = FilteredCommands.FirstOrDefault();
        }

        private void SaveCommand()
        {
            if (SelectedCommand == null) return;

            var configService = GetConfigService();
            var config = configService.LoadConfig();

            config.EmulatorCommands[SelectedCommand.Id] = SelectedCommand;
            configService.SaveConfig(config);

            BackRequested?.Invoke();
        }

        public void GoBack() => BackRequested?.Invoke();

        public void Save() => SaveCommand();
    }
}