using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EMQ.Server.Db.Entities;

[Table("musicbrainz_release_vgmdb_album")]
public class MusicBrainzReleaseVgmdbAlbum
{
    [Dapper.Contrib.Extensions.Key]
    public int id { get; set; }

    [Required]
    public Guid release { get; set; }

    [Required]
    public int album_id { get; set; }
}
