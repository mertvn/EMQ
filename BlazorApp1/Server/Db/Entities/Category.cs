using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlazorApp1.Server.Db.Entities;

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
}
