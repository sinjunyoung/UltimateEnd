using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using UltimateEnd.Models;
using UltimateEnd.Managers;
using System.Threading.Tasks;

namespace UltimateEnd.Services
{
    public class MetadataPersistenceService : IDisposable
    {
        private const int SaveThrottleSeconds = 1;
        private readonly GameMetadataManager _metadataManager;
        private Subject<Unit>? _saveRequested = new();
        private readonly IDisposable? _saveSubscription;
        private bool _disposed;

        public event Action? SaveRequested;

        public MetadataPersistenceService()
        {
            _metadataManager = new GameMetadataManager();
            _saveSubscription = _saveRequested?
                .Throttle(TimeSpan.FromSeconds(SaveThrottleSeconds))
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(_ => ExecuteSave());
        }

        public void MarkGameAsChanged(GameMetadata game)
        {
            if (game == null || string.IsNullOrEmpty(game.PlatformId))
                return;

            _metadataManager.MarkPlatformAsChanged(game.PlatformId);

            try
            {
                _saveRequested?.OnNext(Unit.Default);
            }
            catch (ObjectDisposedException) { }
        }

        public void MarkAsChanged()
        {
            try
            {
                _saveRequested?.OnNext(Unit.Default);
            }
            catch (ObjectDisposedException) { }
        }

        public async Task SaveNowAsync()
        {
            if (!HasUnsavedChanges)
                return;

            await _metadataManager.SaveGamesAsync();
        }

        private void ExecuteSave() => SaveRequested?.Invoke();

        public bool HasUnsavedChanges => _metadataManager.HasChangedPlatforms();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _metadataManager.ClearChangedPlatforms();

            _saveSubscription?.Dispose();
            _saveRequested?.Dispose();
            _saveRequested = null;
        }
    }
}