using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music_source_external_link")]
public class MusicSourceExternalLink
{
    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_source_id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public string url { get; set; } = "";

    [Required]
    public int type { get; set; }

    [Required]
    public string name { get; set; } = "";
}
