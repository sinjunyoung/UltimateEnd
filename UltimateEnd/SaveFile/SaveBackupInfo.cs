using System;

namespace UltimateEnd.SaveFile
{
    public class SaveBackupInfo
    {
        public string FileId { get; set; }

        public string FileName { get; set; }

        public DateTime ModifiedTime { get; set; }

        public SaveBackupMode Mode { get; set; }

        public string DisplayText => Mode switch
        {
            SaveBackupMode.SaveState => $"🎮 스테이트 - {ModifiedTime:yyyy-MM-dd HH:mm}",
            SaveBackupMode.Both => $"💾 전체 - {ModifiedTime:yyyy-MM-dd HH:mm}",
            _ => $"📁 세이브 - {ModifiedTime:yyyy-MM-dd HH:mm}"
        };
    }
}