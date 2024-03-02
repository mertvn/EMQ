using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("users_quiz_settings")]
public class UserQuizSettings
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public int user_id { get; set; }

    [Required]
    public string name { get; set; } = "";

    [Required]
    public string b64 { get; set; } = "";

    [Required]
    public DateTime created_at { get; set; }
}
