using Android.Content;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Android.Dialogs;
using UltimateEnd.Models;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class FilePickerService : FilePickerServiceBase
    {
        private readonly Context _context;
        private readonly PathConverter _pathConverter;

        public FilePickerService(IStorageProvider storageProvider, Context context)
           : base(storageProvider)
        {
            _context = context;
            _pathConverter = new PathConverter(context);
        }

        public override async Task<string?> PickFileAsync(string title, string initialDirectory, FileFilterOptions filterOptions)
        {
            var selectedPath = await FilePickerDialog.ShowAsync(
                title,
                filterOptions.Extensions ?? [],
                _storageProvider,
                initialDirectory
            );

            return selectedPath;
        }

        public override List<FilePickerFileType> ProcessFileTypes(List<FilePickerFileType> fileTypes)
        {
            foreach (var fileType in fileTypes)
            {
                if (fileType.MimeTypes == null || fileType.MimeTypes.Count == 0)
                    fileType.MimeTypes = GetMimeTypesFromPatterns(fileType.Patterns);
            }
            return fileTypes;
        }

        private List<string> GetMimeTypesFromPatterns(IReadOnlyList<string>? patterns)
        {
            if (patterns == null || patterns.Count == 0)
                return ["*/*"];

            var mimeTypes = new List<string>();
            foreach (var pattern in patterns)
            {
                var ext = Path.GetExtension(pattern).ToLower();
                switch (ext)
                {
                    case ".txt":
                        mimeTypes.Add("text/plain");
                        break;
                    case ".xml":
                        mimeTypes.Add("text/xml");
                        mimeTypes.Add("application/xml");
                        break;
                    case ".json":
                        mimeTypes.Add("application/json");
                        break;
                    case ".png":
                    case ".jpg":
                    case ".jpeg":
                    case ".gif":
                    case ".bmp":
                        mimeTypes.Add("image/*");
                        break;
                    default:
                        mimeTypes.Add("*/*");
                        break;
                }
            }
            return mimeTypes.Count > 0 ? mimeTypes : ["*/*"];
        }
    }
}