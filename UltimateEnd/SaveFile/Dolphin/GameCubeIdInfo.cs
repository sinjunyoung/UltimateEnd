namespace UltimateEnd.SaveFile.Dolphin
{
    public class GameCubeIdInfo
    {
        public string GameId { get; set; } = string.Empty;

        public string GameCode { get; set; } = string.Empty;

        public string Region { get; set; } = string.Empty;

        public string RegionFolder { get; set; } = string.Empty;

        public byte RegionCode { get; set; }
    }
}