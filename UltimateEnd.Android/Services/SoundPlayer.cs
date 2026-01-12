using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Services;
using Android.Media;
using System.Threading;

namespace UltimateEnd.Android.Services
{
    public class SoundPlayer : ISoundPlayer
    {
        private MediaPlayer? _mediaPlayer;
        private readonly Lock _lock = new();
        private readonly bool _isDisposed = false;

        public Task PlayAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("사운드 파일을 찾을 수 없습니다.", filePath);

            lock (_lock)
            {
                if (_isDisposed) return Task.CompletedTask;

                try
                {
                    CleanupPlayer();

                    _mediaPlayer = new MediaPlayer();
                    _mediaPlayer.SetDataSource(filePath);

                    _mediaPlayer.Prepared += (s, e) =>
                    {
                        try
                        {
                            _mediaPlayer?.Start();
                        }
                        catch { }
                    };

                    _mediaPlayer.Completion += (s, e) =>
                    {
                        lock (_lock)
                            CleanupPlayer();
                    };

                    _mediaPlayer.PrepareAsync();
                }
                catch
                {
                    CleanupPlayer();
                }
            }

            return Task.CompletedTask;
        }

        private void CleanupPlayer()
        {
            if (_mediaPlayer == null) return;

            try
            {
                if (_mediaPlayer.IsPlaying)
                    _mediaPlayer.Stop();
            }
            catch { }

            try
            {
                _mediaPlayer.Release();
            }
            catch { }

            _mediaPlayer = null;
        }
    }
}