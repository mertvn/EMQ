using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("music_comment")]
public class MusicComment
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int id { get; set; }

    [Required]
    public int music_id { get; set; }

    [Required]
    public int user_id { get; set; }

    [Required]
    [MaxLength(4096)]
    public string comment { get; set; } = "";

    [Required]
    public string[] urls { get; set; } = Array.Empty<string>();

    [Required]
    public SongCommentKind kind { get; set; }

    [Required]
    public DateTime created_at { get; set; }
}

public enum SongCommentKind
{
    Default,

    [Display(Name = "Upload-related")]
    Upload,
    Trumpable,
}
