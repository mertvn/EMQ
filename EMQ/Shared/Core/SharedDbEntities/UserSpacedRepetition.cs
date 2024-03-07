using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("user_spaced_repetition")]
public class UserSpacedRepetition
{
    [Key]
    public int user_id { get; set; }

    [Key]
    public int music_id { get; set; }

    public int n { get; set; }

    public float ease { get; set; } = 2.5f;

    public float interval_days { get; set; }

    public DateTime reviewed_at { get; set; }

    public DateTime due_at { get; set; }
}
