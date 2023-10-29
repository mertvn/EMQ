using System;

namespace EMQ.Shared.Core;

public static class Constants
{
    public static bool UseLocalSongFilesForDevelopment { get; set; } =
        true && Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

    public const string LocalMusicLibraryPath = @"G:/Music";

    public static string ImportDateVndb { get; set; } = "2023-10-29";

    public const string ImportDateEgs = "2023-08-20";

    public const string ImportDateMusicBrainz = "2023-08-19"; // not really accurate as it's rolling

    public const int MaxChatMessageLength = 300;

    public const int MaxGuessLength = 190;

    public const int LinkToleranceSeconds = 17;

    public const string QFDateMin = "1990-01-01";

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
}
