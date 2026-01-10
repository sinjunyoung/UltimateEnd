using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Android.Models;
using UltimateEnd.Android.ViewModels;
using UltimateEnd.Enums;
using UltimateEnd.Utils;
using UltimateEnd.Views.Overlays;

namespace UltimateEnd.Views
{
    public partial class CustomFilePickerControl : BaseOverlay
    {
        private CustomFilePickerViewModel? ViewModel => DataContext as CustomFilePickerViewModel;
        public event EventHandler<string?>? FileSelected;
        public event EventHandler? Cancelled;

        private Grid? _mainGrid;
        private ItemsControl? _fileItemsControl;
        private int _selectedIndex = 0;

        public override bool Visible => _mainGrid?.IsVisible ?? false;

        public CustomFilePickerControl() => InitializeComponent();

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _mainGrid = this.FindControl<Grid>("MainGrid");
            _fileItemsControl = this.FindControl<ItemsControl>("FileItemsControl");
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);

            if (_mainGrid != null)
                _mainGrid.IsVisible = true;

            this.Focusable = true;
            this.Focus();

            _selectedIndex = 0;

            Dispatcher.UIThread.Post(() => UpdateSelection(), DispatcherPriority.Loaded);
        }

        public override void Hide(HiddenState state)
        {
            if (_mainGrid != null)
                _mainGrid.IsVisible = false;

            OnHidden(new HiddenEventArgs { State = state });
        }

        protected override void MovePrevious()
        {
            if (ViewModel?.Files == null || ViewModel.Files.Count == 0)
                return;

            _selectedIndex = Math.Max(0, _selectedIndex - 1);

            try
            {
                UpdateSelection();
            }
            catch { }
        }

        protected override void MoveNext()
        {
            if (ViewModel?.Files == null || ViewModel.Files.Count == 0) return;

            _selectedIndex = Math.Min(ViewModel.Files.Count - 1, _selectedIndex + 1);

            try
            {
                UpdateSelection();
            }
            catch { }
        }

        protected override async void SelectCurrent()
        {
            if (ViewModel?.Files == null || _selectedIndex < 0 || _selectedIndex >= ViewModel.Files.Count)
                return;

            var selectedFile = ViewModel.Files[_selectedIndex];
            ViewModel.SelectedFile = selectedFile;

            if (selectedFile.IsDirectory)
            {
                await ViewModel.OnItemTapped(selectedFile);
                _selectedIndex = 0;
                await Task.Delay(100);
                Dispatcher.UIThread.Post(() => UpdateSelection(), DispatcherPriority.Loaded);
            }
            else
            {
                var path = ViewModel.GetSelectedFilePath();
                FileSelected?.Invoke(this, path);
                Hide(HiddenState.Close);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!this.Visible)
            {
                base.OnKeyDown(e);
                return;
            }

            if (e.Handled) return;

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.ButtonB))
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);

                if (ViewModel?.BreadcrumbPaths != null && ViewModel.BreadcrumbPaths.Count > 1)
                {
                    var parentPath = ViewModel.BreadcrumbPaths[^2];
                    _ = ViewModel.OnBreadcrumbTapped(parentPath);
                    _selectedIndex = 0;
                    Dispatcher.UIThread.Post(() => UpdateSelection(), DispatcherPriority.Loaded);
                }
                else
                {
                    Cancelled?.Invoke(this, EventArgs.Empty);
                    Hide(HiddenState.Cancel);
                }
                return;
            }

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadUp))
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
                MovePrevious();
                return;
            }

            if (InputManager.IsButtonPressed(e.Key, GamepadButton.DPadDown))
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
                MoveNext();
                return;
            }

            if (InputManager.IsAnyButtonPressed(e.Key, GamepadButton.ButtonA, GamepadButton.Start))
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
                SelectCurrent();
                return;
            }

            base.OnKeyDown(e);
        }

        private void UpdateSelection()
        {
            if (ViewModel?.Files == null || _fileItemsControl == null) return;

            if (_selectedIndex >= 0 && _selectedIndex < ViewModel.Files.Count)
                ViewModel.SelectedFile = ViewModel.Files[_selectedIndex];

            var borders = _fileItemsControl.GetVisualDescendants()
                .OfType<Border>()
                .Where(b => b.Name == "FileItemBorder")
                .ToList();

            if (borders.Count == 0) return;

            for (int i = 0; i < borders.Count; i++)
            {
                var border = borders[i];

                if (i == _selectedIndex)
                {
                    border.Background = this.FindResource("Background.Hover") as IBrush ?? Brushes.Gray;
                    border.BringIntoView();
                }
                else
                    border.Background = Brushes.Transparent;
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (ViewModel != null)
            {
                _ = ViewModel.InitializeAsync();
                _selectedIndex = 0;
                Dispatcher.UIThread.Post(() => UpdateSelection(), DispatcherPriority.Loaded);
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);

            Hide(HiddenState.Cancel);
        }

        private async void OnFileTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Border border && border.DataContext is FileItem item)
            {
                var index = ViewModel?.Files?.IndexOf(item) ?? -1;
                if (index >= 0)
                {
                    _selectedIndex = index;
                    UpdateSelection();

                    if (item.IsDirectory)
                    {
                        await ViewModel!.OnItemTapped(item);
                        _selectedIndex = 0;
                        Dispatcher.UIThread.Post(() => UpdateSelection(), DispatcherPriority.Loaded);
                    }
                    else
                    {
                        var path = ViewModel!.GetSelectedFilePath();
                        FileSelected?.Invoke(this, path);
                        Hide(HiddenState.Close);
                    }
                }
            }
        }

        private async void OnBreadcrumbClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is BreadcrumbItem item && ViewModel != null)
            {
                await ViewModel.OnBreadcrumbTapped(item);
                _selectedIndex = 0;
                Dispatcher.UIThread.Post(() => UpdateSelection(), DispatcherPriority.Loaded);
            }
        }

        private async void OnItemLoaded(object? sender, RoutedEventArgs e)
        {
            if (sender is Border border && border.DataContext is FileItem item && ViewModel != null)
                await ViewModel.LoadThumbnailForItem(item);
        }
    }
}