using Android.Media;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.IO;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class SoundPlayer : ISoundPlayer
    {
        private readonly SoundPool _soundPool;
        private readonly ConcurrentDictionary<string, int> _soundIds = new();
        private bool _isDisposed = false;

        public SoundPlayer()
        {
            var audioAttributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Game)
                .SetContentType(AudioContentType.Sonification)
                .Build();

            _soundPool = new SoundPool.Builder()
                .SetMaxStreams(10)
                .SetAudioAttributes(audioAttributes)
                .Build();
        }

        public Task PlayAsync(string filePath)
        {
            if (_isDisposed || !File.Exists(filePath))
                return Task.CompletedTask;

            try
            {
                if (!_soundIds.ContainsKey(filePath))
                {
                    int soundId = _soundPool.Load(filePath, 1);
                    _soundIds[filePath] = soundId;

                    Task.Delay(100).Wait();
                }

                if (_soundIds.TryGetValue(filePath, out int id))
                {
                    _soundPool.Play(id, 1.0f, 1.0f, 1, 0, 1.0f);
                }
            }
            catch { }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _soundPool?.Release();
            _soundPool?.Dispose();
        }
    }
}