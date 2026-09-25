using System;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Library.Entities.Concrete.Dto;

public class ResGetDeveloperStats
{
    public int SongCount { get; set; }

    public PlayerSongStats[] PlayerSongStats { get; set; } = Array.Empty<PlayerSongStats>();
}
