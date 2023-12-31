using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using EMQ.Shared.Core;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[ProtoContract]
public class QuizFilters
{
    [ProtoMember(1)]
    public List<CategoryFilter> CategoryFilters { get; set; } = new();

    [ProtoMember(2)]
    public List<ArtistFilter> ArtistFilters { get; set; } = new();

    [ProtoMember(3)]
    // todo add basic validation here because people keep submitting their user ids :/
    public string VndbAdvsearchFilter { get; set; } = "";

    [ProtoMember(4)]
    public Dictionary<Language, bool> VNOLangs { get; set; } =
        Enum.GetValues<Language>()
            .ToDictionary(x => x, y => y == Language.ja);

    [ProtoMember(5)]
    public Dictionary<SongSourceSongType, IntWrapper> SongSourceSongTypeFilters { get; set; } =
        new()
        {
            { SongSourceSongType.OP, new IntWrapper(0) },
            { SongSourceSongType.ED, new IntWrapper(0) },
            { SongSourceSongType.Insert, new IntWrapper(0) },
            { SongSourceSongType.BGM, new IntWrapper(0) },
            { SongSourceSongType.Random, new IntWrapper(40) },
        };

    [ProtoMember(6)]
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

    [ProtoMember(7)]
    public Dictionary<SongDifficultyLevel, bool> SongDifficultyLevelFilters { get; set; } =
        Enum.GetValues<SongDifficultyLevel>().ToDictionary(x => x, _ => true);

    [ProtoMember(8)]
    [Range(typeof(DateTime), Constants.QFDateMin, Constants.QFDateMax,
        ErrorMessage =
            $"Start date must be in range of {Constants.QFDateMin} to {Constants.QFDateMax}")]
    public DateTime StartDateFilter { get; set; } = DateTime.Parse(Constants.QFDateMin, CultureInfo.InvariantCulture);

    [ProtoMember(9)]
    [Range(typeof(DateTime), Constants.QFDateMin, Constants.QFDateMax,
        ErrorMessage =
            $"End date must be in range of {Constants.QFDateMin} to {Constants.QFDateMax}")]
    public DateTime EndDateFilter { get; set; } = DateTime.Parse(Constants.QFDateMax, CultureInfo.InvariantCulture);

    [ProtoMember(10)]
    [Range(Constants.QFRatingAverageMin, Constants.QFRatingAverageMax)]
    public int RatingAverageStart { get; set; } = Constants.QFRatingAverageMin;

    [ProtoMember(11)]
    [Range(Constants.QFRatingAverageMin, Constants.QFRatingAverageMax)]
    public int RatingAverageEnd { get; set; } = Constants.QFRatingAverageMax;

    [ProtoMember(12)]
    [Range(Constants.QFRatingBayesianMin, Constants.QFRatingBayesianMax)]
    public int RatingBayesianStart { get; set; } = Constants.QFRatingBayesianMin;

    [ProtoMember(13)]
    [Range(Constants.QFRatingBayesianMin, Constants.QFRatingBayesianMax)]
    public int RatingBayesianEnd { get; set; } = Constants.QFRatingBayesianMax;

    // [Range(Constants.QFPopularityMin, Constants.QFPopularityMax)]
    // public int PopularityStart { get; set; } = Constants.QFPopularityMin;
    //
    // [Range(Constants.QFPopularityMin, Constants.QFPopularityMax)]
    // public int PopularityEnd { get; set; } = Constants.QFPopularityMax;

    [ProtoMember(14)]
    [Range(Constants.QFVoteCountMin, Constants.QFVoteCountMax)]
    public int VoteCountStart { get; set; } = Constants.QFVoteCountMin;

    [ProtoMember(15)]
    [Range(Constants.QFVoteCountMin, Constants.QFVoteCountMax)]
    public int VoteCountEnd { get; set; } = Constants.QFVoteCountMax;

    // todo move all applicable filters here
}

[ProtoContract]
// i hate microsoft
public class IntWrapper
{
    /// only for protobuf, do not use
    // ReSharper disable once UnusedMember.Global
    public IntWrapper()
    {
    }

    public IntWrapper(int value)
    {
        Value = value;
    }

    [ProtoMember(1)]
    public int Value { get; set; }
}
