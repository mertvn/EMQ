using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Erodle.Entities.Concrete;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("erodle_users")]
public class ErodleUsers
{
    [Key]
    public int erodle_id { get; set; }

    [Key]
    public int user_id { get; set; }

    [Required]
    public ErodleStatus status { get; set; }
}
