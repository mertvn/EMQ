using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizFilters
{
    public List<CategoryFilter> CategoryFilters { get; set; } = new();

    public Dictionary<SongSourceSongType, bool> SongSourceSongTypeFilters { get; set; } =
        Enum.GetValues<SongSourceSongType>().Where(x => x != SongSourceSongType.Unknown)
            .ToDictionary(x => x, _ => true);

    [Range(typeof(DateTime), Constants.QFDateMin, Constants.QFDateMax,
        ErrorMessage =
            $"Start date must be in range of {Constants.QFDateMin} to {Constants.QFDateMax}")]
    public DateTime StartDateFilter { get; set; } = DateTime.Parse(Constants.QFDateMin);

    [Range(typeof(DateTime), Constants.QFDateMin, Constants.QFDateMax,
        ErrorMessage =
            $"End date must be in range of {Constants.QFDateMin} to {Constants.QFDateMax}")]
    public DateTime EndDateFilter { get; set; } = DateTime.Parse(Constants.QFDateMax);

    public int RatingAverageStart { get; set; } = Constants.QFRatingAverageMin;

    public int RatingAverageEnd { get; set; } = Constants.QFRatingAverageMax;

    public int RatingBayesianStart { get; set; } = Constants.QFRatingBayesianMin;

    public int RatingBayesianEnd { get; set; } = Constants.QFRatingBayesianMax;

    public int PopularityStart { get; set; } = Constants.QFPopularityMin;

    public int PopularityEnd { get; set; } = Constants.QFPopularityMax;

    public int VoteCountStart { get; set; } = Constants.QFVoteCountMin;

    public int VoteCountEnd { get; set; } = Constants.QFVoteCountMax;

    // todo move all applicable filters here
}
