using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("music_music")]
public class MusicMusic
{
    [Key]
    [Required]
    public int source { get; set; }

    [Key]
    [Required]
    public int target { get; set; }

    [Key]
    [Required]
    public MusicMusicRelKind rel { get; set; }
}
