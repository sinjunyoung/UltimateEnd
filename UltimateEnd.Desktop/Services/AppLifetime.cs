using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace UltimateEnd.Desktop.Services
{
    public class AppLifetime : UltimateEnd.Services.IAppLifetime
    {
        public void Shutdown()
        {
            if (Application.Current?.ApplicationLifetime is IControlledApplicationLifetime lifetime)
                lifetime.Shutdown();
        }
    }
}