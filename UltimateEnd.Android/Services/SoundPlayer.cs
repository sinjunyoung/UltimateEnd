using Android.Media;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Services;

namespace UltimateEnd.Android.Services
{
    public class SoundPlayer : ISoundPlayer
    {
        private readonly SoundPool _soundPool;
        private readonly ConcurrentDictionary<string, int> _soundIds = new();
        private readonly ConcurrentDictionary<string, bool> _loadedSounds = new();
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

            _soundPool.LoadComplete += (sender, args) =>
            {
                foreach (var kvp in _soundIds)
                {
                    if (kvp.Value == args.SampleId)
                    {
                        _loadedSounds[kvp.Key] = true;
                        break;
                    }
                }
            };
        }

        public async Task PlayAsync(string filePath)
        {
            if (_isDisposed) return;

            if (!File.Exists(filePath))
                throw new FileNotFoundException("사운드 파일을 찾을 수 없습니다.", filePath);

            try
            {
                if (!_soundIds.ContainsKey(filePath))
                {
                    int soundId = _soundPool.Load(filePath, 1);
                    _soundIds[filePath] = soundId;
                    _loadedSounds[filePath] = false;
                }

                int waitCount = 0;
                while (!_loadedSounds.GetValueOrDefault(filePath, false) && waitCount < 50)
                {
                    await Task.Delay(10);
                    waitCount++;
                }

                if (_loadedSounds.GetValueOrDefault(filePath, false))
                {
                    int soundId = _soundIds[filePath];
                    _soundPool.Play(soundId, 1.0f, 1.0f, 1, 0, 1.0f);
                }
            }
            catch { }
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