using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class FilePickerService(IStorageProvider storageProvider) : FilePickerServiceBase(storageProvider)
    {
        public override async Task<string?> PickFileAsync(string title, string initialDirectory, FileFilterOptions filterOptions)
        {
            var fileType = new FilePickerFileType(filterOptions.DisplayName)
            {
                Patterns = filterOptions.FileNamePatterns ??
                           filterOptions.Extensions?.Select(ext => $"*{ext}").ToArray()
            };

            //IStorageFolder? startLocation = null;

            //if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            //    startLocation = await _storageProvider.TryGetFolderFromPathAsync(initialDirectory);

            var files = await _storageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = [fileType],
                    //SuggestedStartLocation = startLocation
                });

            if (files.Count == 0) return null;

            return files[0].Path.LocalPath;
        }

        public override List<FilePickerFileType> ProcessFileTypes(List<FilePickerFileType> fileTypes) => fileTypes;
    }
}