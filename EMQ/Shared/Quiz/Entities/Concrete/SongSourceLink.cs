using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongSourceLink
{
    public static readonly int[] ProperLinkTypes =
    {
        (int)SongSourceLinkType.VNDB, (int)SongSourceLinkType.ErogamescapeGame,
        (int)SongSourceLinkType.MyAnimeListAnime, (int)SongSourceLinkType.AniListAnime,
        (int)SongSourceLinkType.AniDBAnime, (int)SongSourceLinkType.WikidataItem,
    };

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
