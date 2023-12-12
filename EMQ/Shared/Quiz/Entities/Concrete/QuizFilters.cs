using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizFilters
{
    public List<CategoryFilter> CategoryFilters { get; set; } = new();

    public List<ArtistFilter> ArtistFilters { get; set; } = new();

    // todo add basic validation here because people keep submitting their user ids :/
    public string VndbAdvsearchFilter { get; set; } = "";

    public Dictionary<Language, bool> VNOLangs { get; set; } =
        Enum.GetValues<Language>()
            .ToDictionary(x => x, y => y == Language.ja);

    public Dictionary<SongSourceSongType, IntWrapper> SongSourceSongTypeFilters { get; set; } =
        new()
        {
            { SongSourceSongType.OP, new IntWrapper(0) },
            { SongSourceSongType.ED, new IntWrapper(0) },
            { SongSourceSongType.Insert, new IntWrapper(0) },
            { SongSourceSongType.BGM, new IntWrapper(0) },
            { SongSourceSongType.Random, new IntWrapper(40) },
        };

    public Dictionary<SongSourceSongType, bool> SongSourceSongTypeRandomEnabledSongTypes { get; set; } =
        new()
        {
            { SongSourceSongType.OP, true },
            { SongSourceSongType.ED, true },
            { SongSourceSongType.Insert, true },
            { SongSourceSongType.BGM, true },
        };

    // public Dictionary<SongSourceSongType, IntWrapper> SongSourceSongTypeRandomWeights { get; set; } =
    //     new()
    //     {
    //         { SongSourceSongType.OP, new IntWrapper(100) },
    //         { SongSourceSongType.ED, new IntWrapper(100) },
    //         { SongSourceSongType.Insert, new IntWrapper(100) },
    //         { SongSourceSongType.BGM, new IntWrapper(7) },
    //     };

    public Dictionary<SongDifficultyLevel, bool> SongDifficultyLevelFilters { get; set; } =
        Enum.GetValues<SongDifficultyLevel>().ToDictionary(x => x, _ => true);

    [Range(typeof(DateTime), Constants.QFDateMin, Constants.QFDateMax,
        ErrorMessage =
            $"Start date must be in range of {Constants.QFDateMin} to {Constants.QFDateMax}")]
    public DateTime StartDateFilter { get; set; } = DateTime.Parse(Constants.QFDateMin, CultureInfo.InvariantCulture);

    [Range(typeof(DateTime), Constants.QFDateMin, Constants.QFDateMax,
        ErrorMessage =
            $"End date must be in range of {Constants.QFDateMin} to {Constants.QFDateMax}")]
    public DateTime EndDateFilter { get; set; } = DateTime.Parse(Constants.QFDateMax, CultureInfo.InvariantCulture);

    [Range(Constants.QFRatingAverageMin, Constants.QFRatingAverageMax)]
    public int RatingAverageStart { get; set; } = Constants.QFRatingAverageMin;

    [Range(Constants.QFRatingAverageMin, Constants.QFRatingAverageMax)]
    public int RatingAverageEnd { get; set; } = Constants.QFRatingAverageMax;

    [Range(Constants.QFRatingBayesianMin, Constants.QFRatingBayesianMax)]
    public int RatingBayesianStart { get; set; } = Constants.QFRatingBayesianMin;

    [Range(Constants.QFRatingBayesianMin, Constants.QFRatingBayesianMax)]
    public int RatingBayesianEnd { get; set; } = Constants.QFRatingBayesianMax;

    // [Range(Constants.QFPopularityMin, Constants.QFPopularityMax)]
    // public int PopularityStart { get; set; } = Constants.QFPopularityMin;
    //
    // [Range(Constants.QFPopularityMin, Constants.QFPopularityMax)]
    // public int PopularityEnd { get; set; } = Constants.QFPopularityMax;

    [Range(Constants.QFVoteCountMin, Constants.QFVoteCountMax)]
    public int VoteCountStart { get; set; } = Constants.QFVoteCountMin;

    [Range(Constants.QFVoteCountMin, Constants.QFVoteCountMax)]
    public int VoteCountEnd { get; set; } = Constants.QFVoteCountMax;

    // todo move all applicable filters here
}

// i hate microsoft
public class IntWrapper
{
    public IntWrapper(int value)
    {
        Value = value;
    }

    public int Value { get; set; }
}
