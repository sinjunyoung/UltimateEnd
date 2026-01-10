using Avalonia;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace UltimateEnd.Desktop.Services
{
    public class FolderPicker : UltimateEnd.Services.IFolderPicker
    {
        public async Task<string?> PickFolderAsync(string title, string? defaultPath = null)
        {
            var mainWindow = (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (mainWindow == null)
                return null;

            var folders = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false                
            });

            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        }
    }
}