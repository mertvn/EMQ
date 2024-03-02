using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music_title")]
public class MusicTitle
{
    [Key]
    [Required]
    public int music_id { get; set; }

    [Key]
    [Required]
    public string latin_title { get; set; } = "";

    public string? non_latin_title { get; set; }

    [Key]
    [Required]
    public string language { get; set; } = "";

    [Required]
    public bool is_main_title { get; set; }
}
