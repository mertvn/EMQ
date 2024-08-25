using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Erodle.Entities.Concrete;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("erodle")]
public class Erodle
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int id { get; set; }

    [Required]
    public DateOnly date { get; set; }

    [Required]
    public ErodleKind kind { get; set; }

    [Required]
    public string correct_answer { get; set; } = "";
}
