using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("music_vote")]
public class MusicVote
{
    [Key]
    public int music_id { get; set; }

    [Key]
    public int user_id { get; set; }

    public short? vote { get; set; }

    public DateTime updated_at { get; set; }
}
