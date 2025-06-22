using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("review_queue")]
public class ReviewQueue
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public int music_id { get; set; }

    [Required]
    public string url { get; set; } = "";

    // todo remove because it's unused
    [Required]
    public SongLinkType type { get; set; }

    [Required]
    public bool is_video { get; set; }

    [Required]
    public string submitted_by { get; set; } = "";

    [Required]
    public DateTime submitted_on { get; set; }

    [Required]
    public ReviewQueueStatus status { get; set; }

    public string? reason { get; set; }

    public string? analysis { get; set; }

    public TimeSpan? duration { get; set; }

    public MediaAnalyserResult? analysis_raw { get; set; }

    public string? sha256 { get; set; }

    [Required]
    public SongLinkAttributes attributes { get; set; }

    [Required]
    public SongLinkLineage lineage { get; set; }

    [Required]
    [MaxLength(4096)]
    public string comment { get; set; } = "";
}
