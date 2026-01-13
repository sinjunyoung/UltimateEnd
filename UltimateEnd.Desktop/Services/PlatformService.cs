using Avalonia;
using System.Reflection;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class PlatformService : IPlatformService
    {
        public string GetAppVersion()
        {   
            var version = Assembly.GetExecutingAssembly() .GetName().Version;

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        public string GetAppName()
        {
            var assembly = Assembly.GetExecutingAssembly();

            return assembly.GetName().Name ?? "UltimateEnd";
        }
    }
}