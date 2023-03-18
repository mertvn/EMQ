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

    [Range(typeof(DateTime), Constants.QuizFilterDateMin, Constants.QuizFilterDateMax,
        ErrorMessage =
            $"Start date must be in range of {Constants.QuizFilterDateMin} to {Constants.QuizFilterDateMax}")]
    public DateTime StartDateFilter { get; set; } = DateTime.Parse(Constants.QuizFilterDateMin);

    [Range(typeof(DateTime), Constants.QuizFilterDateMin, Constants.QuizFilterDateMax,
        ErrorMessage =
            $"End date must be in range of {Constants.QuizFilterDateMin} to {Constants.QuizFilterDateMax}")]
    public DateTime EndDateFilter { get; set; } = DateTime.Parse(Constants.QuizFilterDateMax);

    public int RatingAverageStart { get; set; } = 100;

    public int RatingAverageEnd { get; set; } = 1000;

    public int RatingBayesianStart { get; set; } = 100;

    public int RatingBayesianEnd { get; set; } = 1000;

    public int PopularityStart { get; set; } = 0;

    public int PopularityEnd { get; set; } = 10000;

    public int VoteCountStart { get; set; } = 0;

    public int VoteCountEnd { get; set; } = 25000; // todo const

    // todo move all applicable filters here
}
