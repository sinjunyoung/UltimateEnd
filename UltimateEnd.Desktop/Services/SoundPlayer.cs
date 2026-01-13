using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class SoundPlayer : ISoundPlayer, IDisposable
    {
        private static readonly ConcurrentDictionary<string, CachedSound> _cachedSounds = new();
        private readonly WaveOutEvent _outputDevice;
        private readonly MixingSampleProvider _mixer;

        public SoundPlayer()
        {
            _outputDevice = new WaveOutEvent
            {
                DesiredLatency = 100,
                NumberOfBuffers = 2
            };

            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
            {
                ReadFully = true
            };

            _outputDevice.Init(_mixer);
            _outputDevice.Play();
        }

        public Task PlayAsync(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("사운드 파일을 찾을 수 없습니다.", filePath);

            var cachedSound = _cachedSounds.GetOrAdd(filePath, path => new CachedSound(path));
            var provider = new CachedSoundSampleProvider(cachedSound);

            ISampleProvider sampleProvider = provider;

            if (provider.WaveFormat.SampleRate != _mixer.WaveFormat.SampleRate)
                sampleProvider = new WdlResamplingSampleProvider(provider, _mixer.WaveFormat.SampleRate);

            if (sampleProvider.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
                sampleProvider = new MonoToStereoSampleProvider(sampleProvider);

            _mixer.AddMixerInput(sampleProvider);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
        }

        private class CachedSound
        {
            public float[] AudioData { get; }
            public WaveFormat WaveFormat { get; }

            public CachedSound(string audioFileName)
            {
                using var audioFileReader = new AudioFileReader(audioFileName);
                WaveFormat = audioFileReader.WaveFormat;

                int sampleCount = (int)(audioFileReader.Length / 4);
                AudioData = new float[sampleCount];
                audioFileReader.Read(AudioData, 0, sampleCount);
            }
        }

        private class CachedSoundSampleProvider(SoundPlayer.CachedSound cachedSound) : ISampleProvider
        {
            private int _position;

            public int Read(float[] buffer, int offset, int count)
            {
                int available = cachedSound.AudioData.Length - _position;
                int toCopy = Math.Min(available, count);

                if (toCopy > 0)
                {
                    Array.Copy(cachedSound.AudioData, _position, buffer, offset, toCopy);
                    _position += toCopy;
                }

                return toCopy;
            }

            public WaveFormat WaveFormat => cachedSound.WaveFormat;
        }
    }
}