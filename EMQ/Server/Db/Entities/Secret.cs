using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("secret")]
public class Secret
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; } // todo long? use user_id as the key?

    [Required]
    public int user_id { get; set; }

    [Required]
    public string ip_created { get; set; } = "";

    [Required]
    public string ip_last { get; set; } = "";

    [Required]
    public Guid token { get; set; }

    [Required]
    public DateTime created_at { get; set; }

    [Required]
    public DateTime last_used_at { get; set; }
}
