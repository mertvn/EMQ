using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("artist_music")]
public class ArtistMusic
{
    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int artist_id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    public int role { get; set; }

    [Required]
    public int artist_alias_id { get; set; }
}
