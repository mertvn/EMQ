using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongSourceLink
{
    public string Url { get; set; } = "";

    public SongSourceLinkType Type { get; set; } = SongSourceLinkType.Unknown;

    public string Name { get; set; } = "";
}

public enum SongSourceLinkType
{
    Unknown,
    VNDB,
    MusicBrainzRelease,
    VGMdbAlbum,
    ErogamescapeGame,
    MyAnimeListAnime,
    AniListAnime,
    AniDBAnime,
    WikidataItem,
}
