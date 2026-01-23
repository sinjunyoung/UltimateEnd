using System;

namespace UltimateEnd.Updater
{
    public static class UpdaterFactory
    {
        public static Func<IUpdater>? Create { get; set; }
    }
}