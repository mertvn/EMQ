using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorApp1.Server.Db.Entities;

[Table("artist")]
public class Artist
{
    [Dapper.Contrib.Extensions.Key]
    [Required]
    public int id { get; set; }

    public int? sex { get; set; }

    public int? primary_language { get; set; }
}
