using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music")]
public class Music
{
    [Dapper.Contrib.Extensions.Key]
    [Required]
    public int id { get; set; }

    public int? length { get; set; }

    [Required]
    public int type { get; set; }
}
