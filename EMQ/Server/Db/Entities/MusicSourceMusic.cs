using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music_source_music")]
public class MusicSourceMusic
{
    [Key]
    [Required]
    public int music_source_id { get; set; }

    [Key]
    [Required]
    public int music_id { get; set; }

    [Key]
    [Required]
    public int type { get; set; }
}
