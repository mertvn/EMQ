using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("artist_external_link")]
public class ArtistExternalLink
{
    [Key]
    [Required]
    public int artist_id { get; set; }

    [Key]
    [Required]
    public string url { get; set; } = "";

    [Required]
    public SongArtistLinkType type { get; set; }

    [Required]
    public string name { get; set; } = "";
}
