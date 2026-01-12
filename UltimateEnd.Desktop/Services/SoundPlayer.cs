using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class SoundPlayer : ISoundPlayer
    {
        private static readonly ConcurrentDictionary<string, byte[]> _cachedSounds = new();
        private static readonly ConcurrentBag<System.Media.SoundPlayer> _activePlayers = [];

        public Task PlayAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("사운드 파일을 찾을 수 없습니다.", filePath);

            var soundData = _cachedSounds.GetOrAdd(filePath, File.ReadAllBytes);

            var player = new System.Media.SoundPlayer
            {
                Stream = new MemoryStream(soundData)
            };

            _activePlayers.Add(player);
            player.Play();

            return Task.CompletedTask;
        }
    }
}