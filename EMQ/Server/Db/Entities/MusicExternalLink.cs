using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
    public int type { get; set; }

    [Required]
    public bool is_video { get; set; }

    [Required]
    public TimeSpan duration { get; set; }

    public string? submitted_by { get; set; }

    // [Required] // todo
    public string sha256 { get; set; }  = "";
}
