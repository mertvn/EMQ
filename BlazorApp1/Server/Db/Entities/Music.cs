using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorApp1.Server.db.Entities;

[Table("music")]
public class Music
{
    [Dapper.Contrib.Extensions.Key]
    [Required]
    public int id { get; set; }

    [Required]
    public int type { get; set; }
}
