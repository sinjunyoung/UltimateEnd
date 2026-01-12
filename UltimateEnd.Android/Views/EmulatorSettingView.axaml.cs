using Android.Health.Connect.DataTypes;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Linq;
using UltimateEnd.Android.Models;
using UltimateEnd.Android.ViewModels;
using UltimateEnd.Android.Views.Overlays;
using UltimateEnd.Enums;
using UltimateEnd.Models;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Android.Views
{
    public partial class EmulatorSettingView : UserControl
    {
        private EmulatorSettingViewModel ViewModel => DataContext as EmulatorSettingViewModel;
        private IntentExtra _currentExtraForTypePicker;

        private DateTime _lastSoundTime = DateTime.MinValue;
        private const int SOUND_DEBOUNCE_MS = 300;

        public EmulatorSettingView()
        {
            InitializeComponent();
            InitializeOverlays();

            CommandList.KeyDown += OnCommandListKeyDown;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            if (ViewModel != null)
                CommandDetailOverlay.DataContext = ViewModel;

            FocusCommandList();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (DataContext is EmulatorSettingViewModel viewModel)
                CommandDetailOverlay.DataContext = viewModel;
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (ViewModel != null)
                CommandDetailOverlay.DataContext = ViewModel;
        }

        private void InitializeOverlays()
        {
            BaseOverlay[] overlays = {
                AppPickerOverlay,
                ActionPickerOverlay,
                CategoryPickerOverlay,
                ExtraTypePickerOverlay,
                PlatformPickerOverlay,
                FilterPlatformPickerOverlay,
                CommandDetailOverlay,
                TemplateVariablePickerOverlay
            };

            foreach (var overlay in overlays)
            {
                overlay.Showing += async (s, e) =>
                {
                    var now = DateTime.Now;

                    if ((now - _lastSoundTime).TotalMilliseconds > SOUND_DEBOUNCE_MS)
                    {
                        _lastSoundTime = now;
                        await WavSounds.OK();
                    }
                };

                overlay.Hidden += async (s, e) =>
                {
                    if (e.State == HiddenState.Close)
                        await WavSounds.Cancel();
                    if (e.State == HiddenState.Cancel)
                            await WavSounds.Cancel();
                    else if (e.State == HiddenState.Confirm)
                        await WavSounds.OK();

                    FocusCommandList();
                };

                overlay.Click += async (s, e) => await WavSounds.Click();
            }

            AppPickerOverlay.AppSelected += (s, i) =>
            {
                ViewModel.SetSelectedApp(i);
            };

            ActionPickerOverlay.SetTitle("Action 선택");
            ActionPickerOverlay.ItemSelected += (s, action) =>
            {
                if (ViewModel != null)
                    ViewModel.SelectedAction = action;
            };

            CategoryPickerOverlay.SetTitle("Category 선택");
            CategoryPickerOverlay.ItemSelected += (s, category) =>
            {
                if (ViewModel != null)
                    ViewModel.SelectedCategory = category;
            };

            ExtraTypePickerOverlay.SetTitle("Extra Type 선택");
            ExtraTypePickerOverlay.ItemSelected += (s, type) =>
            {
                if (_currentExtraForTypePicker != null)
                {
                    _currentExtraForTypePicker.Type = type;
                    _currentExtraForTypePicker = null!;
                }
            };

            PlatformPickerOverlay.PlatformsConfirmed += (s, selectedIds) =>
            {
                ViewModel.SelectedPlatforms.Clear();
                foreach (var id in selectedIds)
                {
                    ViewModel.SelectedPlatforms.Add(new PlatformTag
                    {
                        Id = ViewModel.GetShortestAlias(id),
                        Image = ViewModel.LoadPlatformImage(GetFullPlatformId(id))
                    });
                }

                var platforms = ViewModel.SelectedPlatforms.Select(p => p.Id).ToList();

                if (ViewModel.SelectedCommand is Models.Command cmd)
                    cmd.SupportedPlatforms = platforms;
            };

            FilterPlatformPickerOverlay.PlatformSelected += (s, platform) =>
            {
                if (ViewModel != null && platform != null)
                    ViewModel.FilterPlatform = platform;
            };

            CommandDetailOverlay.AppPickerRequested += (s, e) =>
            {
                AppPickerOverlay.Show();
            };

            CommandDetailOverlay.ActionPickerRequested += (s, e) =>
            {
                ActionPickerOverlay?.SetItems(ViewModel.AvailableActions, ViewModel.SelectedAction);
                ActionPickerOverlay?.Show();
            };

            CommandDetailOverlay.CategoryPickerRequested += (s, e) =>
            {
                CategoryPickerOverlay?.SetItems(ViewModel.AvailableCategories, ViewModel.SelectedCategory);
                CategoryPickerOverlay?.Show();
            };

            CommandDetailOverlay.ExtraTypePickerRequested += (s, extra) =>
            {
                _currentExtraForTypePicker = extra;
                ExtraTypePickerOverlay?.SetItems(ViewModel.ExtraTypes);
                ExtraTypePickerOverlay?.Show();
            };

            CommandDetailOverlay.PlatformPickerRequested += (s, e) =>
            {
                if (ViewModel?.SelectedCommand == null) return;

                var currentPlatforms = ViewModel.SelectedPlatforms
                    .Select(p => GetFullPlatformId(p.Id))
                    .ToList();

                PlatformPickerOverlay?.SetSelectedPlatforms(currentPlatforms);
                PlatformPickerOverlay?.Show();
            };

            TemplateVariablePickerOverlay.VariableSelected += OnTemplateVariableSelected;
            CommandDetailOverlay.TemplateVariablePickerRequested += (s, e) => TemplateVariablePickerOverlay.Show();
        }

        private async void OnCommandListKeyDown(object? sender, KeyEventArgs e)
        {
            if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                if (ViewModel?.SelectedCommand != null)
                {
                    await WavSounds.OK();
                    CommandDetailOverlay?.Show();
                }
                e.Handled = true;
            }
            else if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                await WavSounds.Cancel();
                ViewModel?.GoBack();
                e.Handled = true;
            }
        }

        private async void OnCommandSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
                await WavSounds.Click();
        }

        private void FocusCommandList()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (CommandList != null && CommandList.IsVisible)
                    CommandList.Focus();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void OnButtonClicked(object? sender, RoutedEventArgs e) => FocusCommandList();

        private void OnShowFilterPlatformClicked(object sender, RoutedEventArgs e)
        {
            FilterPlatformPickerOverlay?.SetSelectedPlatform(ViewModel?.FilterPlatform);
            FilterPlatformPickerOverlay?.Show();
        }

        private void OnCommandTapped(object sender, TappedEventArgs e)
        {
            if (ViewModel?.SelectedCommand != null)
                CommandDetailOverlay?.Show();
        }

        private void OnTemplateVariableSelected(object? sender, string variable) => CommandDetailOverlay.InsertTemplateVariable(variable);

        private static string GetFullPlatformId(string alias)
        {
            try
            {
                var database = UltimateEnd.Services.PlatformInfoService.LoadDatabase();
                var platform = database.Platforms.FirstOrDefault(p => p.Id.Equals(alias, StringComparison.OrdinalIgnoreCase) || (p.Aliases != null && p.Aliases.Any(a => a.Equals(alias, StringComparison.OrdinalIgnoreCase))));

                return platform?.Id ?? alias;
            }
            catch
            {
                return alias;
            }
        }
    }
}