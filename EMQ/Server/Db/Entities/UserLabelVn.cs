using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("users_label_vn")]
public class UserLabelVn
{
    // [Dapper.Contrib.Extensions.Key]
    // [Required]
    // public int id { get; set; }

    [Key]
    [Required]
    public long users_label_id { get; set; }

    [Key]
    [Required]
    public string vnid { get; set; } = "";

    [Required]
    public int vote { get; set; }
}
