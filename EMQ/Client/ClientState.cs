using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Client;

public static class ClientState
{
    public static Session? Session { get; set; }

    public static ServerStats ServerStats { get; set; } = new();

    public static TimeSpan Countdown { get; set; }

    public static PlayerVndbInfo VndbInfo { get; set; } = new();

    public static Dictionary<string, PeriodicTimer> Timers { get; set; } = new();

    public static PlayerPreferences Preferences { get; set; } = new();

    public static ConcurrentDictionary<string, UploadResult> UploadResults { get; set; } = new();

    public static Dictionary<int, MusicVote> MusicVotes { get; set; } = new();

    public static Dictionary<int, SongArtist> ArtistsCache { get; } = new(); // todo? move to ArtistComponent

    public static SongArtist[] CopiedCAL { get; set; } = Array.Empty<SongArtist>();
}
