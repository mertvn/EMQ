using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("users_label")]
public class UserLabel
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public long id { get; set; }

    [Required]
    public int user_id { get; set; }

    [Required]
    public string vndb_uid { get; set; } = "";

    [Required]
    public int vndb_label_id { get; set; }

    [Required]
    public string vndb_label_name { get; set; } = "";

    [Required]
    public bool vndb_label_is_private { get; set; }

    [Required]
    public int kind { get; set; }
}
