﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("artist_alias")]
public class ArtistAlias
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public int artist_id { get; set; }

    [Required]
    public string latin_alias { get; set; } = "";

    public string? non_latin_alias { get; set; }

    public bool is_main_name { get; set; }
}
