using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("music_source")]
public class MusicSource
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int id { get; set; }

    [Required]
    public DateTime air_date_start { get; set; }

    public DateTime? air_date_end { get; set; }

    [Required]
    public string language_original { get; set; } = "";

    public int? rating_average { get; set; }

    public int? rating_bayesian { get; set; }

    // public int? popularity { get; set; }

    public int? votecount { get; set; }

    [Required]
    public SongSourceType type { get; set; }

    public List<SongSourceDeveloper>? developers { get; set; }
}
