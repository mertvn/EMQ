using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("verification_register")]
public class VerificationRegister
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public string username { get; set; } = "";

    [Required]
    public string email { get; set; } = "";

    [Required]
    public string token { get; set; } = "";

    [Required]
    public DateTime created_at { get; set; }
}
