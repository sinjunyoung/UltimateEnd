using System;
using System.Threading.Tasks;

namespace UltimateEnd.Updater
{
    public interface IUpdater
    {
        Task PerformUpdateAsync(GitHubRelease release, IProgress<UpdateProgress> progress = null);
    }
}