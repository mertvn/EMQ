using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

// protobuf is bigger/slower than json for this
public class ResGetPublicUserInfoSongs
{
    public ResMostPlayedSongs[] MostPlayedSongs { get; set; } = Array.Empty<ResMostPlayedSongs>();

    public ResCommonPlayers[] CommonPlayers { get; set; } = Array.Empty<ResCommonPlayers>();

    public ResUserMusicVotes[] UserMusicVotes { get; set; } = Array.Empty<ResUserMusicVotes>();
}

public class ResMostPlayedSongs
{
    public int MusicId { get; set; }

    public int Played { get; set; }

    public int Correct { get; set; }

    public float CorrectPercentage => ((float)Correct).Div0(Played) * 100;

    public int IntervalDays { get; set; }

    public SongMini SongMini { get; set; } = new();
}

public class ResCommonPlayers
{
    public UserLite UserLite { get; set; } = new();

    public int QuizCount { get; set; }

    [JsonIgnore]
    public int UserId { get; set; }
}

public class ResUserMusicVotes
{
    public SongMini SongMini { get; set; } = new();

    public MusicVote MusicVote { get; set; } = new();

    public bool IsBGM { get; set; }
}
