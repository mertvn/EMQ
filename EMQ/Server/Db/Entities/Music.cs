using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Dapper.Database.Attributes;

namespace EMQ.Server.Db.Entities;

[Table("music")]
public class Music
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public int type { get; set; }

    [Required]
    public long stat_correct { get; set; }

    [Required]
    public long stat_played { get; set; }

    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [IgnoreInsert, IgnoreUpdate]
    public float stat_correctpercentage { get; set; }

    [Required]
    public long stat_guessed { get; set; }

    [Required]
    public long stat_totalguessms { get; set; }

    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [IgnoreInsert, IgnoreUpdate]
    public int stat_averageguessms { get; set; }

    public Guid? musicbrainz_recording_gid { get; set; }

    [Required]
    public int stat_uniqueusers { get; set; }

    [Required]
    public int attributes { get; set; }
}
