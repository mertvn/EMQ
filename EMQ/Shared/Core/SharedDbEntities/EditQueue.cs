using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EMQ.Shared.Auth.Entities.Concrete.Dto.Response;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Core.SharedDbEntities;

[Table("edit_queue")]
public class EditQueue
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Required]
    public int id { get; set; }

    [Required]
    public string submitted_by { get; set; } = "";

    [Required]
    public DateTime submitted_on { get; set; }

    [Required]
    public ReviewQueueStatus status { get; set; }

    [Required]
    public EntityKind entity_kind { get; set; }

    [Required]
    public int entity_version { get; set; } // todo protobuf and remove

    [Required]
    public string entity_json { get; set; } = ""; // todo important switch to using protobuf instead

    public string? old_entity_json { get; set; } // todo important switch to using protobuf instead

    public string? note_user { get; set; }

    public string? note_mod { get; set; }

    [Required]
    public int entity_id { get; set; }
}

public class ResGetSongSource
{
    public SongSource SongSource { get; set; } = new();

    public PlayerSongStats[] PlayerSongStats { get; set; } = Array.Empty<PlayerSongStats>();
}

public class ResGetSongArtist
{
    public List<SongArtist> SongArtists { get; set; } = new();

    public PlayerSongStats[] PlayerSongStats { get; set; } = Array.Empty<PlayerSongStats>();
}

public class ReqEditSong
{
    public ReqEditSong(Song song, bool isNew, string? noteUser)
    {
        Song = song;
        IsNew = isNew;
        NoteUser = noteUser;
    }

    [Required]
    public Song Song { get; }

    [Required]
    public bool IsNew { get; }

    public string? NoteUser { get; }
}

public enum EntityKind
{
    None,
    Song,
    SongSource,
    SongArtist,
    MergeArtists,
    DeleteSong,
}

public class ReqEditArtist
{
    public ReqEditArtist(SongArtist artist, bool isNew, string? noteUser)
    {
        Artist = artist;
        IsNew = isNew;
        NoteUser = noteUser;
    }

    [Required]
    public SongArtist Artist { get; }

    [Required]
    public bool IsNew { get; }

    public string? NoteUser { get; }
}

public class ReqEditSource
{
    public ReqEditSource(SongSource source, bool isNew, string? noteUser)
    {
        Source = source;
        IsNew = isNew;
        NoteUser = noteUser;
    }

    [Required]
    public SongSource Source { get; }

    [Required]
    public bool IsNew { get; }

    public string? NoteUser { get; }
}

public class ReqGetEntityHistory
{
    public ReqGetEntityHistory(EntityKind entityKind, int entityId)
    {
        EntityKind = entityKind;
        EntityId = entityId;
    }

    public EntityKind EntityKind { get; }

    public int EntityId { get; }
}
