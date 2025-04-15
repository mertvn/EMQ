using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Dapper.Database.Attributes;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Server.Db.Entities;

[Table("music")]
public class Music
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public SongType type { get; set; }

    [Required]
    public SongAttributes attributes { get; set; }

    [Required]
    public DataSourceKind data_source { get; set; }
}
