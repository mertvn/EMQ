using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("users_nekobako")]
public class UserNekobako
{
    [Key]
    [Required]
    public Guid id { get; set; }

    [Required]
    public string extension { get; set; } = "";

    [Required]
    public int user_id { get; set; }

    [Required]
    public long size_bytes { get; set; }

    [Required]
    public string sha256 { get; set; } = "";

    [Required]
    public string orig_name { get; set; } = "";

    [Required]
    public DateTime uploaded_at { get; set; }
}
