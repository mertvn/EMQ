using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("music_source_external_link")]
public class MusicSourceExternalLink
{
    [Key]
    [Required]
    public int music_source_id { get; set; }

    [Key]
    [Required]
    public string url { get; set; } = "";

    [Required]
    public SongSourceLinkType type { get; set; }

    [Required]
    public string name { get; set; } = "";
}
