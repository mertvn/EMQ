using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("erodle_history")]
public class ErodleHistory
{
    [Key]
    public int erodle_id { get; set; }

    [Key]
    public int user_id { get; set; }

    [Key]
    public int sp { get; set; }

    [Required]
    public string guess { get; set; } = "";
}
