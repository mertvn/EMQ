using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("review_queue")]
public class ReviewQueue
{
    [Dapper.Contrib.Extensions.Key]
    [Required]
    public int id { get; set; }

    [Dapper.Contrib.Extensions.ExplicitKey]
    [Required]
    public int music_id { get; set; }

    [Required]
    public string url { get; set; }

    [Required]
    public int type { get; set; }

    [Required]
    public bool is_video { get; set; }

    [Required]
    public string submitted_by { get; set; }

    [Required]
    public DateTime submitted_on { get; set; }

    [Required]
    public int status { get; set; }

    public string? reason { get; set; }
}
