using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("quiz")]
public class EntityQuiz
{
    [Key]
    public Guid id { get; set; }

    [Key]
    public Guid room_id { get; set; }

    public string settings_b64 { get; set; } = "";

    public bool should_update_stats { get; set; }

    public DateTime created_at { get; set; }
}
