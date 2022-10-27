using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorApp1.Server.Db.Entities;

[Table("music_title")]
public class MusicTitle
{
    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public string latin_title { get; set; }

    public string? non_latin_title { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int language { get; set; }

    [Required]
    public bool is_main_title { get; set; }
}
