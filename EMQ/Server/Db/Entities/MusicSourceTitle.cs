using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music_source_title")]
public class MusicSourceTitle
{
    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_source_id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public string latin_title { get; set; }

    public string? non_latin_title { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public string language { get; set; } = "";

    [Required]
    public bool is_main_title { get; set; }
}
