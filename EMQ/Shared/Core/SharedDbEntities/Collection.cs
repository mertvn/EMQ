using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("collection")]
public class Collection
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int id { get; set; }

    [Required]
    [MaxLength(128)]
    public string name { get; set; } = "";

    [Required]
    public EntityKind entity_kind { get; set; }

    [Required]
    public DateTime created_at { get; set; }
}
