using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music_source_category")]
public class MusicSourceCategory
{
    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_source_id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int category_id { get; set; }
}
