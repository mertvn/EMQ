using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Dapper.Database.Attributes;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("music_stat")]
public class MusicStat
{
    [Key]
    [Required]
    public int music_id { get; set; }

    [Key]
    [Required]
    public GuessKind guess_kind { get; set; }

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

    [Required]
    public int stat_uniqueusers { get; set; }
}
