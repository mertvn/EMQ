using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using EMQ.Shared.Core;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

// todo move all applicable filters here
[ProtoContract]
public class QuizFilters
{
    [ProtoMember(1)]
    public List<CategoryFilter> CategoryFilters { get; set; } = new();

    [ProtoMember(2)]
    public List<ArtistFilter> ArtistFilters { get; set; } = new();

    [ProtoMember(3)]
    [CustomValidation(typeof(QuizSettings), nameof(QuizSettings.ValidateVndbAdvsearchFilter))]
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
            { SongSourceSongType.Other, new IntWrapper(0) },
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
            { SongSourceSongType.Other, false },
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

    [ProtoMember(16)]
    [Required]
    [DefaultValue(false)]
    public bool OnlyOwnUploads { get; set; } = false;

    [ProtoMember(17)]
    [Required]
    [DefaultValue(ScreenshotKind.None)]
    public ScreenshotKind ScreenshotKind { get; set; } = ScreenshotKind.None;

    [ProtoMember(18)]
    [Required]
    [DefaultValue(Constants.QFStartTimePercentageMin)]
    [Range(Constants.QFStartTimePercentageMin, Constants.QFStartTimePercentageMax)]
    public int StartTimePercentageStart { get; set; } = Constants.QFStartTimePercentageMin;

    [ProtoMember(19)]
    [Required]
    [DefaultValue(Constants.QFStartTimePercentageMax)]
    [Range(Constants.QFStartTimePercentageMin, Constants.QFStartTimePercentageMax)]
    public int StartTimePercentageEnd { get; set; } = Constants.QFStartTimePercentageMax;

    [ProtoMember(20)]
    [Range(Constants.QFSongRatingAverageMin, Constants.QFSongRatingAverageMax)]
    public int SongRatingAverageStart { get; set; } = Constants.QFSongRatingAverageMin;

    [ProtoMember(21)]
    [Range(Constants.QFSongRatingAverageMin, Constants.QFSongRatingAverageMax)]
    public int SongRatingAverageEnd { get; set; } = Constants.QFSongRatingAverageMax;

    [ProtoMember(22)]
    [Range(Constants.QFSongRatingAverageMin, Constants.QFSongRatingAverageMax)]
    public int OwnersSongRatingAverageStart { get; set; } = Constants.QFSongRatingAverageMin;

    [ProtoMember(23)]
    [Range(Constants.QFSongRatingAverageMin, Constants.QFSongRatingAverageMax)]
    public int OwnersSongRatingAverageEnd { get; set; } = Constants.QFSongRatingAverageMax;

    [ProtoMember(24)]
    public Dictionary<ListReadKind, IntWrapper> ListReadKindFilters { get; set; } =
        new()
        {
            { ListReadKind.Read, new IntWrapper(40) },
            { ListReadKind.Unread, new IntWrapper(0) },
            { ListReadKind.Random, new IntWrapper(0) },
        };

    [ProtoMember(25)]
    [Required]
    [DefaultValue(MusicVoteStatusKind.All)]
    public MusicVoteStatusKind OwnersMusicVoteStatus { get; set; } = MusicVoteStatusKind.All;

    [ProtoMember(26)]
    public Dictionary<SongAttributes, LabelKind> SongAttributesTrileans { get; set; } =
        new()
        {
            { SongAttributes.Spoilers, LabelKind.Maybe },
            { SongAttributes.NonCanon, LabelKind.Maybe },
            { SongAttributes.Unofficial, LabelKind.Maybe },
            { SongAttributes.FlashingLights, LabelKind.Maybe },
        };

    [ProtoMember(27)]
    public Dictionary<SongType, LabelKind> SongTypeTrileans { get; set; } =
        new()
        {
            { SongType.Standard, LabelKind.Maybe },
            { SongType.Instrumental, LabelKind.Maybe },
            { SongType.Image, LabelKind.Maybe },
            { SongType.Cover, LabelKind.Maybe },
        };

    [ProtoMember(28)]
    [Required]
    [DefaultValue(false)]
    public bool IsPreferLongLinks { get; set; } = false;

    public bool ListReadKindFiltersIsOnlyRead =>
        ListReadKindFilters.TryGetValue(ListReadKind.Read, out var val) && val.Value > 0 &&
        !ListReadKindFilters.Where(x => x.Key != ListReadKind.Read).Any(x => x.Value.Value > 0);

    public bool ListReadKindFiltersIsAllRandom =>
        ListReadKindFilters.TryGetValue(ListReadKind.Random, out var val) && val.Value > 0 &&
        !ListReadKindFilters.Where(x => x.Key != ListReadKind.Random).Any(x => x.Value.Value > 0);

    public bool ListReadKindFiltersHasUnread =>
        ListReadKindFilters.TryGetValue(ListReadKind.Unread, out var val) && val.Value > 0;

    public bool CanHaveBGM =>
        SongSourceSongTypeFilters.TryGetValue(SongSourceSongType.BGM, out var b) && b.Value > 0 ||
        (SongSourceSongTypeRandomEnabledSongTypes.TryGetValue(SongSourceSongType.BGM, out bool rb) && rb &&
         SongSourceSongTypeFilters.TryGetValue(SongSourceSongType.Random, out var r) && r.Value > 0);
}

public enum ListReadKind // todo? find a better name
{
    Read,
    Unread,
    Random,
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
