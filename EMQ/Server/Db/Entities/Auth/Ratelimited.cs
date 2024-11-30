using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities.Auth;

[Table("ratelimited")]
public class Ratelimited
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public string ip { get; set; } = "";

    [Required]
    public string action { get; set; } = "";

    [Required]
    public DateTime created_at { get; set; }
}
