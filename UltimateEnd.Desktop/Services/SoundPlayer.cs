using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UltimateEnd.Services;

namespace UltimateEnd.Desktop.Services
{
    public class SoundPlayer : ISoundPlayer
    {
        public Task PlayAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("사운드 파일을 찾을 수 없습니다.", filePath);

            var player = new System.Media.SoundPlayer(filePath);

            player.LoadCompleted += (s, e) =>
            {
                try
                {
                    player.Play();
                }
                catch(System.Exception ex)
                {
                    Debug.Write(ex.ToString());
                }
            };

            player.LoadAsync();

            return Task.CompletedTask;
        }
    }
}