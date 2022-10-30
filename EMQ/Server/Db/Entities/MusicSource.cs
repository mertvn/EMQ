using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("music_source")]
public class MusicSource
{
    [Dapper.Contrib.Extensions.Key]
    public int id { get; set; }

    [Required]
    public DateTime air_date_start { get; set; }

    public DateTime? air_date_end { get; set; }

    [Required]
    public int language_original { get; set; }

    public int? rating_average { get; set; }

    [Required]
    public int type { get; set; }
}
