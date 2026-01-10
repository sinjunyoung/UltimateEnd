using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class PathValidator : IPathValidator
    {
        public bool ValidatePath(string path) => !string.IsNullOrEmpty(path) && Directory.Exists(path);
    }
}