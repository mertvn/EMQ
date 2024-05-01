using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("users_label_preset")]
public class UserLabelPreset
{
    [Key]
    [Required]
    public int user_id { get; set; }

    [Key]
    [Required]
    public string name { get; set; } = "";

    [Required]
    public bool is_active { get; set; }
}
