using SQLite;

namespace UltimateEnd.Scraper.Models
{
    [Table("games")]
    public class GameRecord
    {
        [PrimaryKey]
        [Column("rom_file")]
        public string RomFile { get; set; }

        [Column("title")]
        public string Title { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("parent_rom_file")]
        public string ParentRomFile { get; set; }

        [Column("has_korean")]
        public int HasKorean { get; set; }

        public bool IsKorean => HasKorean == 1;
    }
}