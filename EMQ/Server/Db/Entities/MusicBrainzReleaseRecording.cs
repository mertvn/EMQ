using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("musicbrainz_release_recording")]
public class MusicBrainzReleaseRecording
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int id { get; set; }

    [Required]
    public Guid release { get; set; }

    [Required]
    public Guid recording { get; set; }
}
