using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("category")]
public class Category
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public SongSourceCategoryType type { get; set; }

    public string? vndb_id { get; set; }
}
