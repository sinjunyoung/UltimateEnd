using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;
using UltimateEnd.Models;

namespace UltimateEnd.Services
{
    public abstract class FilePickerServiceBase : IFilePickerService
    {
        protected readonly IStorageProvider _storageProvider;

        protected FilePickerServiceBase(IStorageProvider storageProvider)
        {
            _storageProvider = storageProvider;
        }

        public abstract Task<string?> PickFileAsync(string title, string initialDirectory, FileFilterOptions filterOptions);

        public abstract List<FilePickerFileType> ProcessFileTypes(List<FilePickerFileType> fileTypes);
    }
}