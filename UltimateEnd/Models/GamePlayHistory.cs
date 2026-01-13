using SQLite;
using System;

namespace UltimateEnd.Models
{
    public class GamePlayHistory
    {
        [PrimaryKey]
        public string Id { get; set; }

        [Indexed]
        public string Platform { get; set; }

        public string GameFileName { get; set; }

        public string SubFolder { get; set; }

        public DateTime? LastPlayedTime { get; set; }

        public long TotalPlayTimeSeconds { get; set; }

        public DateTime? CurrentSessionStart { get; set; }

        public bool IsPlaying { get; set; }

        [Ignore]
        public TimeSpan TotalPlayTime
        {
            get => TimeSpan.FromSeconds(TotalPlayTimeSeconds);
            set => TotalPlayTimeSeconds = (long)value.TotalSeconds;
        }
    }
}