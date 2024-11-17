using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("artist_artist")]
public class ArtistArtist
{
    [Key]
    [Required]
    public int source { get; set; }

    [Key]
    [Required]
    public int target { get; set; }

    [Key]
    [Required]
    public ArtistArtistRelKind rel { get; set; }
}
