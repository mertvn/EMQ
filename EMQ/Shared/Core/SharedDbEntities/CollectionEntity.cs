using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("collection_entity")]
public class CollectionEntity
{
    [Key]
    [Required]
    public int collection_id { get; set; }

    [Key]
    [Required]
    public int entity_id { get; set; }

    [Required]
    public DateTime modified_at { get; set; }

    [Required]
    public int modified_by { get; set; }
}
