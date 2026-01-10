using Android.OS;

namespace UltimateEnd.Android.Services
{
    public class AppLifetime : UltimateEnd.Services.IAppLifetime
    {
        public void Shutdown()
        {
            Process.KillProcess(Process.MyPid());
        }
    }
}