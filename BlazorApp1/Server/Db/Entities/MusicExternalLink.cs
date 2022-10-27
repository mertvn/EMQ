using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorApp1.Server.Db.Entities;

[Table("music_external_link")]
public class MusicExternalLink
{
    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public string url { get; set; }

    [Required]
    public int type { get; set; }

    [Required]
    public bool is_video { get; set; }
}
