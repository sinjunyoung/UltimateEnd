using System;
using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class AssetPathProvider : IAssetPathProvider
    {
        public string GetAssetPath(string subFolder, string fileName) => Path.Combine(AppContext.BaseDirectory, "Assets", subFolder, fileName);
    }
}