using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("room")]
public class EntityRoom
{
    [Key]
    public Guid id { get; set; }

    public string initial_name { get; set; } = "";

    public int created_by { get; set; }

    public DateTime created_at { get; set; }
}
