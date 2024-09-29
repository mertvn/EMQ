using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("artist_music")]
public class ArtistMusic
{
    [Key]
    [Required]
    public int artist_id { get; set; }

    [Key]
    [Required]
    public int music_id { get; set; }

    [Key]
    public SongArtistRole role { get; set; }

    [Required]
    public int artist_alias_id { get; set; }
}
