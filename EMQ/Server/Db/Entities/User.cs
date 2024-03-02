using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("users")]
public class User
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public string username { get; set; } = "";

    [Required]
    public string email { get; set; } = "";

    [Required]
    public int roles { get; set; }

    [Required]
    public DateTime created_at { get; set; }

    [Required]
    public string salt { get; set; } = "";

    [Required]
    public string hash { get; set; } = "";
}
