using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("category")]
public class Category
{
    [Dapper.Contrib.Extensions.Key]
    [Required]
    public int id { get; set; }

    [Required]
    public string name { get; set; }

    [Required]
    public int type { get; set; }

    public string? vndb_id { get; set; }
}
