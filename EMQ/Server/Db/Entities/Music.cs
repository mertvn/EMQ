using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music")]
public class Music
{
    [Dapper.Contrib.Extensions.Key]
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
    [Dapper.Contrib.Extensions.Write(false)]
    public float stat_correctpercentage { get; set; }

    [Required]
    public long stat_guessed { get; set; }

    [Required]
    public long stat_totalguessms { get; set; }

    [Required]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    [Dapper.Contrib.Extensions.Write(false)]
    public int stat_averageguessms { get; set; }
}
