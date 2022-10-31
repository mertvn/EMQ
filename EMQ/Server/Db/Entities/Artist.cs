using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("artist")]
public class Artist
{
    [Dapper.Contrib.Extensions.Key]
    [Required]
    public int id { get; set; }

    public int? sex { get; set; }

    public string? primary_language { get; set; }

    public string? vndb_id { get; set; }
}
