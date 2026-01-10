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
    }
}