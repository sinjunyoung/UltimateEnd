using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Services;
using UltimateEnd.Models;

namespace UltimateEnd.Utils
{
    public static class DialogHelper
    {
        static IStorageProvider? StorageProvider;

        public static async Task<string?> OpenFileAsync(string initialDirectory, params List<FilePickerFileType> filters)
        {
            var storageProvider = GetStorageProvider();
            if (storageProvider == null)
                return null;

            var fileFilters = filters ?? [FilePickerFileTypes.All];
            var filePickerService = FilePickerServiceFactory.Create?.Invoke(storageProvider);

            if (filePickerService == null)
                return null;

            fileFilters = filePickerService.ProcessFileTypes(fileFilters);

            var filterOptions = new FileFilterOptions
            {
                DisplayName = fileFilters.FirstOrDefault()?.Name ?? "파일 선택",
                Extensions = fileFilters.FirstOrDefault()?.Patterns?
                    .Select(p => p.Replace("*", string.Empty))
                    .ToArray() ?? []
            };

            return await filePickerService.PickFileAsync("파일 선택", initialDirectory, filterOptions);
        }

        private static IStorageProvider? GetStorageProvider()
        {
            if (StorageProvider != null)
                return StorageProvider;

            var lifetime = Application.Current?.ApplicationLifetime;
            if (lifetime is IClassicDesktopStyleApplicationLifetime desktop)
                StorageProvider = desktop.MainWindow?.StorageProvider;
            else if (lifetime is ISingleViewApplicationLifetime singleView)
                StorageProvider = TopLevel.GetTopLevel(singleView.MainView)?.StorageProvider;

            return StorageProvider;
        }

        public static void SetStorageProvider(IStorageProvider provider) => StorageProvider = provider;

        public static void ResetStorageProvider() => StorageProvider = null;
    }
}