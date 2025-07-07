using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("music_external_link")]
public class MusicExternalLink
{
    [Key]
    [Required]
    public int music_id { get; set; }

    [Key]
    [Required]
    public string url { get; set; } = "";

    [Required]
    public SongLinkType type { get; set; }

    [Required]
    public bool is_video { get; set; }

    [Required]
    public TimeSpan duration { get; set; }

    public string? submitted_by { get; set; }

    // [Required] // todo
    public string sha256 { get; set; } = "";

    public MediaAnalyserResult? analysis_raw { get; set; }

    [Required]
    public SongLinkAttributes attributes { get; set; }

    [Required]
    public SongLinkLineage lineage { get; set; }

    [Required]
    [MaxLength(4096)]
    public string comment { get; set; } = "";

    public TimeRange[] vocals_ranges { get; set; } = Array.Empty<TimeRange>();
}
