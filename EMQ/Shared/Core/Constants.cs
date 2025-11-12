using System;
using System.Collections.Generic;
using EMQ.Shared.Core.SharedDbEntities;
using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Core;

public static class Constants
{
    public static string? SelfhostAddress { get; set; } = Environment.GetEnvironmentVariable("EMQ_SELFHOST_ADDRESS");

    public const string WebsiteName = "Eroge Music Quiz";

    public const string WebsiteDomain = "https://erogemusicquiz.com";

    public const string WebsiteDomainNoProtocol = "erogemusicquiz.com";

    public const string LocalMusicLibraryPath = @"G:\Music";

    public const string ImportDateVndb = "2025-05-06";

    public const string ImportDateEgs = "2024-09-25";

    public const string ImportDateMusicBrainz = "2023-08-19"; // not really accurate as it's rolling

    public const string ImportDateMusicBrainzActual = "2025-02-04";

    public const string ImportDateVgmdb = "2025-02-03";

    public const string AnalysisOkStr = "OK";

    public const string RobotName = "Cookie 4IS"; // todo no space

    public static readonly string RobotNameLower = RobotName.ToLowerInvariant();

    public const int MaxChatMessageLength = 300;

    public const int MaxGuessLength = 190;

    public const int LinkToleranceSeconds = 17;

    public const string SHDateMin = "2024-02-17";

    public const string ErodleDateMin = "2024-08-25";

    public const int ErodleMaxGuesses = 17;

    public const int ErodleMinVotes = 250;

    public const int SHUseLastNPlaysPerPlayer = 3;

    public const int PlayerIdGuestMin = 1_000_000;

    public const int PlayerIdBotMin = 2_000_000_000;

    public static readonly Dictionary<EntityKind, int> EntityVersionsDict = new()
    {
        { EntityKind.Song, 2 },
        { EntityKind.SongArtist, 1 },
        { EntityKind.MergeArtists, 1 },
        { EntityKind.SongSource, 1 },
        { EntityKind.DeleteSong, 1 },
    };

    public static readonly Dictionary<SongSourceType, SongSourceLinkType[]> ValidLinkTypesForSourceTypeDict = new()
    {
        { SongSourceType.Unknown, Array.Empty<SongSourceLinkType>() },
        { SongSourceType.VN, new[] { SongSourceLinkType.VNDB, SongSourceLinkType.ErogamescapeGame, SongSourceLinkType.WikidataItem } },
        { SongSourceType.Other, Array.Empty<SongSourceLinkType>() },
        { SongSourceType.Anime, new[] { SongSourceLinkType.MyAnimeListAnime, SongSourceLinkType.AniListAnime, SongSourceLinkType.AniDBAnime } },
        { SongSourceType.Touhou, new[] { SongSourceLinkType.ErogamescapeGame, SongSourceLinkType.WikidataItem } },
        { SongSourceType.Game, new[] { SongSourceLinkType.ErogamescapeGame, SongSourceLinkType.WikidataItem } },
    };

    public const string QFDateMin = "1987-01-01";

    public const string QFDateMax = "2030-01-01";

    public const int QFRatingAverageMin = 100;

    public const int QFRatingAverageMax = 1000;

    public const int QFRatingBayesianMin = 100;

    public const int QFRatingBayesianMax = 1000;

    // public const int QFPopularityMin = 0;
    //
    // public const int QFPopularityMax = 10000;

    public const int QFVoteCountMin = 0;

    public const int QFVoteCountMax = 25000;

    public const int QFStartTimePercentageMin = 0;

    public const int QFStartTimePercentageMax = 100;

    public const int QFSongRatingAverageMin = 100;

    public const int QFSongRatingAverageMax = 1000;
}
