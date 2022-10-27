using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorApp1.Server.db.Entities;

[Table("artist_alias")]
public class ArtistAlias
{
    [Dapper.Contrib.Extensions.Key]
    [Required]
    public int id { get; set; }

    [Required]
    public int artist_id { get; set; }

    [Required]
    public string latin_alias { get; set; }

    public string? non_latin_alias { get; set; }

    public bool? is_main_name { get; set; }
}
