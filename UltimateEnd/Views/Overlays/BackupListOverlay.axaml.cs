using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using UltimateEnd.Enums;
using UltimateEnd.Helpers;
using UltimateEnd.SaveFile;
using UltimateEnd.ViewModels;

namespace UltimateEnd.Views.Overlays
{
    public partial class BackupListOverlay : BaseOverlay
    {
        private TaskCompletionSource<string> _tcs;
        private List<SaveBackupInfo> _backups;
        private int _selectedIndex = 0;
        private readonly ObservableCollection<BackupItemViewModel> _backupItems;

        public override bool Visible => MainGrid.IsVisible;

        public BackupListOverlay()
        {
            InitializeComponent();
            _backupItems = [];
            BackupList.ItemsSource = _backupItems;
        }

        public Task<string> ShowBackupListAsync(string gameName, List<SaveBackupInfo> backups)
        {
            _tcs = new TaskCompletionSource<string>();

            GameNameText.Text = gameName;
            _backups = backups;
            _selectedIndex = 0;

            _backupItems.Clear();
            for (int i = 0; i < backups.Count; i++)
            {
                var backup = backups[i];
                _backupItems.Add(new BackupItemViewModel
                {
                    FileId = backup.FileId,
                    FileName = backup.FileName,
                    DisplayName = i == 0 ? $"{backup.DisplayText} (ÃÖ½Å)" : backup.DisplayText,
                    ModifiedTime = backup.ModifiedTime,
                    IconKey = backup.IconKey,
                    IsSelected = i == 0
                });
            }

            Show();

            return _tcs.Task;
        }

        public override void Show()
        {
            OnShowing(EventArgs.Empty);
            MainGrid.IsVisible = true;
            this.Focusable = true;
            this.Focus();
        }

        public override void Hide(HiddenState state)
        {
            MainGrid.IsVisible = false;

            if (state == HiddenState.Confirm && _selectedIndex >= 0 && _selectedIndex < _backupItems.Count)
                _tcs?.TrySetResult(_backupItems[_selectedIndex].FileId);
            else
                _tcs?.TrySetResult(null);

            OnHidden(new HiddenEventArgs { State = state });
        }

        protected async override void SelectCurrent()
        {
            if (_backupItems.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _backupItems.Count)
            {
                await WavSounds.OK();
                Hide(HiddenState.Confirm);
            }
        }

        protected override void MovePrevious()
        {
            if (_backupItems.Count == 0) return;

            _backupItems[_selectedIndex].IsSelected = false;
            _selectedIndex = (_selectedIndex - 1 + _backupItems.Count) % _backupItems.Count;
            _backupItems[_selectedIndex].IsSelected = true;
        }

        protected override void MoveNext()
        {
            if (_backupItems.Count == 0) return;

            _backupItems[_selectedIndex].IsSelected = false;
            _selectedIndex = (_selectedIndex + 1) % _backupItems.Count;
            _backupItems[_selectedIndex].IsSelected = true;
        }

        private async void OnBackupItemClick(object sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is BackupItemViewModel item)
            {
                var index = _backupItems.IndexOf(item);

                if (index >= 0)
                {
                    await WavSounds.OK();

                    _backupItems[_selectedIndex].IsSelected = false;
                    _selectedIndex = index;
                    _backupItems[_selectedIndex].IsSelected = true;
                }
            }

            e.Handled = true;
        }

        private void OnRestoreClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Hide(HiddenState.Confirm);
            e.Handled = true;
        }

        private void OnCancelClick(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            Hide(HiddenState.Cancel);
            e.Handled = true;
        }

        private void OnBackgroundClick(object sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender)
            {
                Hide(HiddenState.Cancel);
                e.Handled = true;
            }
        }
    }
}