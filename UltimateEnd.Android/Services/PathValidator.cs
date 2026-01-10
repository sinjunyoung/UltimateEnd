using AndroidX.DocumentFile.Provider;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class PathValidator : IPathValidator
    {
        public bool ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                var converter = new PathConverter(AndroidApplication.AppContext);
                var safUri = converter.FriendlyPathToUri(path);

                if (safUri.StartsWith("content://"))
                {
                    var uri = global::Android.Net.Uri.Parse(safUri);
                    var context = global::Android.App.Application.Context;
                    var docFile = DocumentFile.FromTreeUri(context, uri);

                    return docFile != null && docFile.Exists();
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}