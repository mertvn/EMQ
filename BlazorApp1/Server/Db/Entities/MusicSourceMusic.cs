using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorApp1.Server.db.Entities;

[Table("music_source_music")]
public class MusicSourceMusic
{
    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_source_id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_id { get; set; }

    [Required]
    public int type { get; set; }
}
