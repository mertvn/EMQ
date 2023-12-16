using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("secret")]
public class Secret
{
    [Dapper.Contrib.Extensions.Key]
    [Required]
    public int id { get; set; }

    [Required]
    public int user_id { get; set; }

    [Required]
    public Guid token { get; set; }

    [Required]
    public DateTime created_at { get; set; }

    [Required]
    public DateTime last_used_at { get; set; }
}
