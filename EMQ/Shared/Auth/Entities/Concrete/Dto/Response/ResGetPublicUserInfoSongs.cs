using System;
using System.Collections.Generic;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Auth.Entities.Concrete.Dto.Response;

public class ResGetPublicUserInfoSongs
{
    public ResMostPlayedSongs[] MostPlayedSongs { get; set; } = Array.Empty<ResMostPlayedSongs>();

    public ResCommonPlayers[] CommonPlayers { get; set; } = Array.Empty<ResCommonPlayers>();
}

public class ResMostPlayedSongs
{
    public int MusicId { get; set; }

    public int Played { get; set; }

    public int IntervalDays { get; set; }

    public Song Song { get; set; } = new();
}

public class ResCommonPlayers
{
    public UserLite UserLite { get; set; } = new();

    public int QuizCount { get; set; }
}
