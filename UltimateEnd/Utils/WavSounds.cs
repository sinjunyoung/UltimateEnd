using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Services;

namespace UltimateEnd.Utils
{
    public static class WavSounds
    {
        public const int MillisecondsPerSecond = 1000;
        private static IAssetPathProvider? _pathProvider;
        private static ISoundPlayer? _currentPlayer;
        private static readonly Dictionary<string, int> _durationCache = [];

        public static void Initialize(IAssetPathProvider pathProvider) => _pathProvider = pathProvider;

        public static class Durations
        {
            public static int AppClosing => GetCachedDuration("app.closing.wav");

            public static int OK => GetCachedDuration("ok.wav");

            public static int Complete => GetCachedDuration("complete.wav");

            public static int Confirm => OK;

            public static int Coin => GetCachedDuration("coin.wav");

            public static int Click => GetCachedDuration("click.wav");

            public static int Cancel => GetCachedDuration("cancel.wav");

            public static int Error => GetCachedDuration("error.wav");

            public static int Success => GetCachedDuration("launch.wav");
        }

        public static async Task AppClosing() => await PlaySound("app.closing.wav");

        public static async Task OK() => await PlaySound("ok.wav");

        public static async Task Complete() => await PlaySound("complete.wav");

        public static async Task Confirm() => await OK();

        public static async Task Coin() => await PlaySound("coin.wav");

        public static async Task Click() => await PlaySound("click.wav");

        public static async Task Cancel() => await PlaySound("cancel.wav");

        public static async Task Error() => await PlaySound("error.wav");

        public static async Task Success() => await PlaySound("launch.wav");

        public static async Task PlaySound(string fileName)
        {
            if (_pathProvider == null)
                throw new InvalidOperationException("WavSounds가 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.");

            try
            {
                string soundPath = _pathProvider.GetAssetPath("Sounds", fileName);
                _currentPlayer = SoundPlayerFactory.Create();
                await _currentPlayer.PlayAsync(soundPath);
            }
            catch { }
        }

        public static async Task PlaySoundAndWait(string fileName, int durationMs)
        {
            await PlaySound(fileName);
            await Task.Delay(durationMs);
        }

        private static int GetCachedDuration(string fileName)
        {
            if (_durationCache.TryGetValue(fileName, out int duration))
                return duration;

            duration = GetWavDuration(fileName);
            _durationCache[fileName] = duration;
            return duration;
        }

        public static int GetWavDuration(string fileName)
        {
            if (_pathProvider == null)
                return MillisecondsPerSecond;

            try
            {
                string filePath = _pathProvider.GetAssetPath("Sounds", fileName);
                using var reader = new BinaryReader(File.OpenRead(filePath));

                string riff = new(reader.ReadChars(4));
                if (riff != "RIFF") return MillisecondsPerSecond;

                reader.ReadInt32();
                string wave = new(reader.ReadChars(4));
                if (wave != "WAVE") return MillisecondsPerSecond;

                string fmt = new(reader.ReadChars(4));
                int fmtSize = reader.ReadInt32();
                reader.ReadInt16();
                int channels = reader.ReadInt16();
                int sampleRate = reader.ReadInt32();
                int byteRate = reader.ReadInt32();
                int blockAlign = reader.ReadInt16();
                int bitsPerSample = reader.ReadInt16();

                if (fmtSize > 16)
                    reader.BaseStream.Seek(fmtSize - 16, SeekOrigin.Current);

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string chunkId = new(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "data")
                    {
                        double durationSeconds = (double)chunkSize / byteRate;
                        return (int)(durationSeconds * MillisecondsPerSecond);
                    }
                    else
                        reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                }

                return MillisecondsPerSecond;
            }
            catch
            {
                return MillisecondsPerSecond;
            }
        }
    }
}