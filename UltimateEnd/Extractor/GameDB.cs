using SQLite;

namespace UltimateEnd.Extractor
{
    [Table("Games")]
    public class Game
    {
        [PrimaryKey]
        public string GameId { get; set; } // "001-BAZE" 형식

        public int? PlatformId { get; set; } // 외래키 (원래대로)

        public string GameCode { get; set; } // "BAZE"

        public string? Name { get; set; }
        public string? NameEn { get; set; }
        public string? Developer { get; set; }
        public string? Publisher { get; set; }
        public string? ReleaseDate { get; set; }

        public string GenreId { get; set; }
        public string? Languages { get; set; }
        public string? Region { get; set; }
        public string? Description { get; set; }
    }
}