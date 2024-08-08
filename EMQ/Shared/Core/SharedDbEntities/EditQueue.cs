using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
}

public class ResGetSongSource
{
    public SongSource SongSource { get; set; }
}

public class ResGetSongArtist
{
    public List<SongArtist> SongArtists { get; set; }
}

public class ReqGetSongArtist
{
    public ReqGetSongArtist(int aId
        // ,int aaId
    )
    {
        AId = aId;
        // AAId = aaId;
    }

    public int AId { get; set; }

    // public int AAId { get; set; }
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
}

public class EditQueueContainer
{
    public EditQueue EditQueue { get; set; }

    public string OldEntityJson { get; set; }
}
