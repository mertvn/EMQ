using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

// todo? rename to musicbrainz_recording_track
[Table("musicbrainz_track_recording")]
public class MusicBrainzTrackRecording
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int id { get; set; }

    [Required]
    public Guid track { get; set; }

    [Required]
    public Guid recording { get; set; }
}
