using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music_source_category")]
public class MusicSourceCategory
{
    [Key]
    [Required]
    public int music_source_id { get; set; }

    [Key]
    [Required]
    public int category_id { get; set; }

    public float? rating { get; set; }

    public int? spoiler_level { get; set; }
}
