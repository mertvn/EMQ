using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizFilters
{
    public List<CategoryFilter> CategoryFilters { get; set; } = new();

    public List<ArtistFilter> ArtistFilters { get; set; } = new();

    public string VndbAdvsearchFilter { get; set; } = "";

    public Dictionary<Language, bool> VNOLangs { get; set; } =
        Enum.GetValues<Language>()
            .ToDictionary(x => x, y => y == Language.ja);

    public Dictionary<SongSourceSongType, bool> SongSourceSongTypeFilters { get; set; } =
        Enum.GetValues<SongSourceSongType>()
            .Where(x => x is SongSourceSongType.OP or SongSourceSongType.ED or SongSourceSongType.Insert)
            .ToDictionary(x => x, _ => true);

    [Range(typeof(DateTime), Constants.QFDateMin, Constants.QFDateMax,
        ErrorMessage =
            $"Start date must be in range of {Constants.QFDateMin} to {Constants.QFDateMax}")]
    public DateTime StartDateFilter { get; set; } = DateTime.Parse(Constants.QFDateMin);

    [Range(typeof(DateTime), Constants.QFDateMin, Constants.QFDateMax,
        ErrorMessage =
            $"End date must be in range of {Constants.QFDateMin} to {Constants.QFDateMax}")]
    public DateTime EndDateFilter { get; set; } = DateTime.Parse(Constants.QFDateMax);

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
