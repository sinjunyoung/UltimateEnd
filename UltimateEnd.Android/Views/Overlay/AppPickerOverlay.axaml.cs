using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Android.Models;
using UltimateEnd.Android.Services;
using UltimateEnd.Enums;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Android.Views.Overlays
{
    public partial class AppPickerOverlay : BaseOverlay
    {
        public override bool Visible => MainGrid.IsVisible;
        public event EventHandler<InstalledAppInfo>? AppSelected;

        private readonly ObservableCollection<AppItemViewModel> _apps = [];
        private List<AppItemViewModel> _filteredApps = [];
        private int _selectedIndex = 0;
        private bool _isLoaded = false;

        public AppPickerOverlay()
        {
            InitializeComponent();
            SearchBox.TextChanged += OnSearchTextChanged;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible) return;

            if (SearchBox.IsFocused &&
                !InputManager.IsAnyButtonPressed(e.Key,
                    GamepadButton.ButtonB,
                    GamepadButton.ButtonA,
                    GamepadButton.Start,
                    GamepadButton.DPadUp,
                    GamepadButton.DPadDown))
                return;

            base.OnKeyDown(e);
        }

        protected override void MovePrevious()
        {
            if (_filteredApps.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _filteredApps.Count) % _filteredApps.Count;
            UpdateSelection();
        }

        protected override void MoveNext()
        {
            if (_filteredApps.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _filteredApps.Count;
            UpdateSelection();
        }

        protected override void SelectCurrent()
        {
            if (_filteredApps.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _filteredApps.Count)
            {
                var current = _filteredApps[_selectedIndex];
                SelectApp(current);
            }
        }

        private void SelectApp(AppItemViewModel app)
        {
            var appInfo = new InstalledAppInfo
            {
                DisplayName = app.DisplayName,
                PackageName = app.PackageName,
                ActivityName = app.ActivityName,
                Icon = app.Icon
            };

            AppSelected?.Invoke(this, appInfo);
            Hide(HiddenState.Close);
        }

        private void UpdateSelection()
        {
            var borders = AppItemsControl?.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.DataContext is AppItemViewModel)
                .ToList();

            if (borders == null || borders.Count == 0) return;

            for (int i = 0; i < borders.Count; i++)
            {
                var border = borders[i];
                if (i == _selectedIndex)
                {
                    border.Background = this.FindResource("Background.Hover") as IBrush;
                    border.BringIntoView();
                }
                else
                    border.Background = this.FindResource("Background.Secondary") as IBrush;
            }
        }

        private async Task LoadAppsAsync()
        {
            if (_isLoaded) return;

            try
            {
                LoadingPanel.IsVisible = true;
                AppScrollViewer.IsVisible = false;

                await Task.Run(() =>
                {
                    var service = new InstalledAppsService();
                    var apps = service.GetInstalledApps();

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        _apps.Clear();
                        foreach (var app in apps)
                        {
                            _apps.Add(new AppItemViewModel
                            {
                                DisplayName = app.DisplayName,
                                PackageName = app.PackageName,
                                ActivityName = app.ActivityName,
                                Icon = app.Icon
                            });
                        }

                        _filteredApps = _apps.ToList();
                        AppItemsControl.ItemsSource = _filteredApps;

                        LoadingPanel.IsVisible = false;
                        AppScrollViewer.IsVisible = true;

                        _isLoaded = true;
                    });
                });
            }
            catch
            {
                LoadingPanel.IsVisible = false;
                AppScrollViewer.IsVisible = true;
            }
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            var searchText = SearchBox.Text?.ToLower() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredApps = _apps.ToList();
            }
            else
            {
                _filteredApps = _apps.Where(a =>
                    a.DisplayName.ToLower().Contains(searchText) ||
                    a.PackageName.ToLower().Contains(searchText) ||
                    a.ActivityName.ToLower().Contains(searchText)).ToList();
            }

            AppItemsControl.ItemsSource = _filteredApps;
            _selectedIndex = 0;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateSelection(),
                Avalonia.Threading.DispatcherPriority.Loaded);
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();

            _selectedIndex = 0;

            _ = LoadAppsAsync();
        }

        public override void Hide(HiddenState state)
        {
            Avalonia.Threading.DispatcherTimer.RunOnce(() => MainGrid.IsVisible = false, TimeSpan.FromMilliseconds(300));
            OnHidden(new HiddenEventArgs { State = state });
        }

        private void OnAppItemTapped(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is AppItemViewModel vm)
            {
                SelectApp(vm);
                OnClick(EventArgs.Empty);
            }

            e.Handled = true;
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Hide(HiddenState.Cancel);
            e.Handled = true;
        }

        private void OnClose(object? sender, PointerPressedEventArgs e)
        {
            Hide(HiddenState.Cancel);
            e.Handled = true;
        }

        private void OnBackgroundClick(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
                Hide(HiddenState.Cancel);
        }
    }
}