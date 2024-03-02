using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("verification_forgottenpassword")]
public class VerificationForgottenPassword
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public int user_id { get; set; }

    [Required]
    public string token { get; set; } = "";

    [Required]
    public DateTime created_at { get; set; }
}
