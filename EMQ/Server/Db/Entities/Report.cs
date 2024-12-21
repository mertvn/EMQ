using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Library.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("report")]
public class Report
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public int music_id { get; set; }

    [Required]
    public string url { get; set; } = "";

    [Required]
    public SongReportKind report_kind { get; set; }

    [Required]
    public string submitted_by { get; set; } = "";

    [Required]
    public DateTime submitted_on { get; set; }

    [Required]
    public ReviewQueueStatus status { get; set; }

    public string note_mod { get; set; } = "";

    public string note_user { get; set; } = "";
}
