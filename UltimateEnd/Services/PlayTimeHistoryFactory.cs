using System;
using System.IO;
using System.Threading.Tasks;

namespace UltimateEnd.Services
{
    public static class PlayTimeHistoryFactory
    {
        const string DatabaseFileName = "playtime.db";

        private static PlayTimeHistory _instance;
        private static readonly object _lock = new();

        public static PlayTimeHistory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            string dbPath = GetDatabaseFilePath();
                            var directory = Path.GetDirectoryName(dbPath);

                            if (!Directory.Exists(directory))
                                Directory.CreateDirectory(directory);

                            _instance = new PlayTimeHistory(dbPath);
                        }
                    }
                }
                return _instance;
            }
        }

        public static async Task StopAllActiveSessions() => await _instance.StopAllActiveSessions();

        private static string GetDatabaseFilePath()
        {
            var provider = AppBaseFolderProviderFactory.Create?.Invoke();

            if (provider != null)
                return Path.Combine(provider.GetSettingsFolder(), DatabaseFileName);

            return Path.Combine(AppContext.BaseDirectory, DatabaseFileName);
        }
    }
}