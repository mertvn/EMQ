using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using EMQ.Shared.Core;
using ProtoBuf;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[ProtoContract]
public class QuizSettings
{
    [ProtoMember(1)]
    [Required]
    [Range(1, 100)]
    [DefaultValue(40)]
    public int NumSongs { get; set; } = 40;

    [ProtoMember(2)]
    [Required]
    [Range(5000, 60000)]
    [DefaultValue(25000)]
    public int GuessMs { get; set; } = 25000;

    [Required]
    [Range(5, 60)]
    [DefaultValue(25)]
    // ReSharper disable once InconsistentNaming
    public int UI_GuessMs
    {
        get { return GuessMs / 1000; }
        set { GuessMs = value * 1000; }
    }

    [ProtoMember(3)]
    [Required]
    [Range(5000, 60000)]
    [DefaultValue(25000)]
    public int ResultsMs { get; set; } = 25000;

    [Required]
    [Range(5, 60)]
    [DefaultValue(25)]
    // ReSharper disable once InconsistentNaming
    public int UI_ResultsMs
    {
        get { return ResultsMs / 1000; }
        set { ResultsMs = value * 1000; }
    }

    [ProtoMember(4)]
    [Required]
    [Range(1, 1)]
    [DefaultValue(1)]
    public int PreloadAmount { get; set; } = 1;

    [ProtoMember(5)]
    [Required]
    [DefaultValue(true)]
    public bool IsHotjoinEnabled { get; set; } = true;

    [ProtoMember(6)]
    [Required]
    [Range(1, 8)]
    [DefaultValue(1)]
    public int TeamSize { get; set; } = 1;

    [ProtoMember(7)]
    [Required]
    [DefaultValue(false)]
    public bool Duplicates { get; set; } = false;

    [ProtoMember(8)]
    [Required]
    [Range(0, 9)]
    [DefaultValue(0)]
    public int MaxLives { get; set; } = 0;

    [ProtoMember(9)]
    [Required]
    [DefaultValue(true)]
    public bool OnlyFromLists { get; set; } = true;

    [ProtoMember(10)]
    [Required]
    [DefaultValue(SongSelectionKind.Random)]
    public SongSelectionKind SongSelectionKind { get; set; } = SongSelectionKind.Random;

    [ProtoMember(11)]
    [Required]
    [DefaultValue(AnsweringKind.Typing)]
    public AnsweringKind AnsweringKind { get; set; } = AnsweringKind.Typing;

    [ProtoMember(12)]
    [Required]
    [Range(10000, 250000)]
    [DefaultValue(120000)]
    public int LootingMs { get; set; } = 120000;

    [Required]
    [Range(10, 250)]
    [DefaultValue(120)]
    // ReSharper disable once InconsistentNaming
    public int UI_LootingMs
    {
        get { return LootingMs / 1000; }
        set { LootingMs = value * 1000; }
    }

    [ProtoMember(13)]
    [Required]
    [Range(1, 25)]
    [DefaultValue(7)]
    public int InventorySize { get; set; } = 7;

    [ProtoMember(14)]
    [Required]
    [Range(51, 100)]
    [DefaultValue(90)]
    public int WaitPercentage { get; set; } = 90;

    [ProtoMember(15)]
    [Required]
    [Range(5000, 250000)]
    [DefaultValue(30000)]
    public int TimeoutMs { get; set; } = 30000;

    [Required]
    [Range(5, 250)]
    [DefaultValue(150)]
    // ReSharper disable once InconsistentNaming
    public int UI_TimeoutMs
    {
        get { return TimeoutMs / 1000; }
        set { TimeoutMs = value * 1000; }
    }

    [CustomValidation(typeof(QuizSettings), nameof(ValidateSongSourceSongTypeFiltersSum))]
    public int SongSourceSongTypeFiltersSum => Filters.SongSourceSongTypeFilters.Sum(x => x.Value.Value);

    [ProtoMember(16)]
    [Required]
    public QuizFilters Filters { get; set; } = new();

    [ProtoMember(17)]
    [Required]
    [DefaultValue(ListDistributionKind.Random)]
    public ListDistributionKind ListDistributionKind { get; set; } = ListDistributionKind.Random;

    [ProtoMember(18)]
    [Required]
    [DefaultValue(GamemodeKind.Default)]
    public GamemodeKind GamemodeKind { get; set; } = GamemodeKind.Default;

    [ProtoMember(19)]
    [Required]
    [DefaultValue(true)]
    public bool NGMCAllowBurning { get; set; } = true;

    [ProtoMember(20)]
    [Required]
    [DefaultValue(false)]
    public bool AllowViewingInventoryDuringQuiz { get; set; } = false; // todo diff

    [ProtoMember(21)]
    [Required]
    [DefaultValue(true)]
    public bool NGMCAutoPickOnlyCorrectPlayerInTeam { get; set; } = true; // todo diff

    [ProtoMember(22)]
    [Required]
    [DefaultValue(SpacedRepetitionKind.Review)]
    public SpacedRepetitionKind SpacedRepetitionKind { get; set; } = SpacedRepetitionKind.Review; // todo diff

    public static ValidationResult ValidateSongSourceSongTypeFiltersSum(int sum, ValidationContext validationContext)
    {
        if (sum == 0)
        {
            return new ValidationResult("The sum of selected song types must be greater than 0.",
                new[] { validationContext.MemberName! });
        }

        PropertyInfo numSongsProperty = validationContext.ObjectType.GetProperty(nameof(NumSongs))!;
        int numSongsPropertyValue = (int)numSongsProperty.GetValue(validationContext.ObjectInstance, null)!;

        if (sum > numSongsPropertyValue)
        {
            return new ValidationResult("The sum of selected song types cannot be greater than the number of songs.",
                new[] { validationContext.MemberName! });
        }

        return ValidationResult.Success!;
    }

    public static ValidationResult ValidateVndbAdvsearchFilter(string str, ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(str))
        {
            if (str.Contains("/u", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(str, "u[1-9]{1,9}$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            {
                return new ValidationResult("Read the explanation again.",
                    new[] { validationContext.MemberName! });
            }
        }

        return ValidationResult.Success!;
    }

    // new is a reserved keyword ¯\_(ツ)_/¯
    public static List<string> Diff(QuizSettings o, QuizSettings n)
    {
        var diff = new List<string>();
        if (o.NumSongs != n.NumSongs)
        {
            diff.Add($"Maximum songs: {o.NumSongs} → {n.NumSongs}");
        }

        if (o.UI_GuessMs != n.UI_GuessMs)
        {
            diff.Add($"Guess time: {o.UI_GuessMs} → {n.UI_GuessMs}");
        }

        if (o.UI_ResultsMs != n.UI_ResultsMs)
        {
            diff.Add($"Results time: {o.UI_ResultsMs} → {n.UI_ResultsMs}");
        }

        if (o.IsHotjoinEnabled != n.IsHotjoinEnabled)
        {
            diff.Add($"Enable hotjoin: {o.IsHotjoinEnabled} → {n.IsHotjoinEnabled}");
        }

        if (o.TeamSize != n.TeamSize)
        {
            diff.Add($"Maximum team size: {o.TeamSize} → {n.TeamSize}");
        }

        if (o.Duplicates != n.Duplicates)
        {
            diff.Add($"Duplicates: {o.Duplicates} → {n.Duplicates}");
        }

        if (o.MaxLives != n.MaxLives)
        {
            diff.Add($"Lives: {o.MaxLives} → {n.MaxLives}");
        }

        if (o.OnlyFromLists != n.OnlyFromLists)
        {
            diff.Add($"Only from lists: {o.OnlyFromLists} → {n.OnlyFromLists}");
        }

        if (o.SongSelectionKind != n.SongSelectionKind)
        {
            diff.Add(
                $"Song selection method: {o.SongSelectionKind.GetDescription()} → {n.SongSelectionKind.GetDescription()}");
        }

        if (o.AnsweringKind != n.AnsweringKind)
        {
            diff.Add($"Answering method: {o.AnsweringKind.GetDescription()} → {n.AnsweringKind.GetDescription()}");
        }

        if (o.UI_LootingMs != n.UI_LootingMs)
        {
            diff.Add($"Looting time: {o.UI_LootingMs} → {n.UI_LootingMs}");
        }

        if (o.InventorySize != n.InventorySize)
        {
            diff.Add($"Looting inventory size: {o.InventorySize} → {n.InventorySize}");
        }

        if (o.WaitPercentage != n.WaitPercentage)
        {
            diff.Add($"Wait percentage: {o.WaitPercentage} → {n.WaitPercentage}");
        }

        if (o.UI_TimeoutMs != n.UI_TimeoutMs)
        {
            diff.Add($"Timeout time: {o.UI_TimeoutMs} → {n.UI_TimeoutMs}");
        }

        // todo
        if (JsonSerializer.Serialize(o.Filters.ArtistFilters) != JsonSerializer.Serialize(n.Filters.ArtistFilters))
        {
            diff.Add($"Artists were modified.");
        }

        // todo
        if (JsonSerializer.Serialize(o.Filters.CategoryFilters) != JsonSerializer.Serialize(n.Filters.CategoryFilters))
        {
            // todo non-tag categories may be added in the future
            diff.Add($"Tags were modified.");
        }

        if (o.Filters.VndbAdvsearchFilter != n.Filters.VndbAdvsearchFilter)
        {
            diff.Add($"VNDB search filter: {o.Filters.VndbAdvsearchFilter} → {n.Filters.VndbAdvsearchFilter}");
        }

        if (JsonSerializer.Serialize(o.Filters.VNOLangs) != JsonSerializer.Serialize(n.Filters.VNOLangs))
        {
            var ol = o.Filters.VNOLangs
                .Where(x => x.Value)
                .Select(y => y.Key.GetDisplayName());

            var ne = n.Filters.VNOLangs
                .Where(x => x.Value)
                .Select(y => y.Key.GetDisplayName());

            diff.Add($"VN original languages: {string.Join(", ", ol)} → {string.Join(", ", ne)}");
        }

        if (JsonSerializer.Serialize(o.Filters.SongSourceSongTypeFilters) !=
            JsonSerializer.Serialize(n.Filters.SongSourceSongTypeFilters))
        {
            // string d = "";
            foreach ((SongSourceSongType key, IntWrapper? oldValue) in o.Filters.SongSourceSongTypeFilters)
            {
                var newValue = n.Filters.SongSourceSongTypeFilters[key];
                if (oldValue.Value != newValue.Value)
                {
                    diff.Add($"{key}: {oldValue.Value} → {newValue.Value}");
                    // d += $"{key}: {oldValue.Value} → {newValue.Value}";
                }
            }
        }

        if (JsonSerializer.Serialize(o.Filters.SongSourceSongTypeRandomEnabledSongTypes) !=
            JsonSerializer.Serialize(n.Filters.SongSourceSongTypeRandomEnabledSongTypes))
        {
            var ol = o.Filters.SongSourceSongTypeRandomEnabledSongTypes
                .Where(x => x.Value)
                .Select(y => y.Key);

            var ne = n.Filters.SongSourceSongTypeRandomEnabledSongTypes
                .Where(x => x.Value)
                .Select(y => y.Key);

            diff.Add($"Random song types: {string.Join(", ", ol)} → {string.Join(", ", ne)}");
        }

        if (JsonSerializer.Serialize(o.Filters.SongDifficultyLevelFilters) !=
            JsonSerializer.Serialize(n.Filters.SongDifficultyLevelFilters))
        {
            var ol = o.Filters.SongDifficultyLevelFilters
                .Where(x => x.Value)
                .Select(y => y.Key.GetDisplayName());

            var ne = n.Filters.SongDifficultyLevelFilters
                .Where(x => x.Value)
                .Select(y => y.Key.GetDisplayName());

            diff.Add($"Song difficulties: {string.Join(", ", ol)} → {string.Join(", ", ne)}");
        }

        if (o.Filters.StartDateFilter != n.Filters.StartDateFilter ||
            o.Filters.EndDateFilter != n.Filters.EndDateFilter)
        {
            diff.Add(
                $"VN date: {o.Filters.StartDateFilter:yyyy-MM-dd} - {o.Filters.EndDateFilter:yyyy-MM-dd} → {n.Filters.StartDateFilter:yyyy-MM-dd} - {n.Filters.EndDateFilter:yyyy-MM-dd}");
        }

        if (o.Filters.RatingAverageStart != n.Filters.RatingAverageStart ||
            o.Filters.RatingAverageEnd != n.Filters.RatingAverageEnd)
        {
            diff.Add(
                $"VN rating (average): {o.Filters.RatingAverageStart / 100f:N2} - {o.Filters.RatingAverageEnd / 100f:N2} → {n.Filters.RatingAverageStart / 100f:N2} - {n.Filters.RatingAverageEnd / 100f:N2}");
        }

        if (o.Filters.RatingBayesianStart != n.Filters.RatingBayesianStart ||
            o.Filters.RatingBayesianEnd != n.Filters.RatingBayesianEnd)
        {
            diff.Add(
                $"VN rating (bayesian): {o.Filters.RatingBayesianStart / 100f:N2} - {o.Filters.RatingBayesianEnd / 100f:N2} → {n.Filters.RatingBayesianStart / 100f:N2} - {n.Filters.RatingBayesianEnd / 100f:N2}");
        }

        if (o.Filters.VoteCountStart != n.Filters.VoteCountStart ||
            o.Filters.VoteCountEnd != n.Filters.VoteCountEnd)
        {
            diff.Add(
                $"VN vote count: {o.Filters.VoteCountStart} - {o.Filters.VoteCountEnd} → {n.Filters.VoteCountStart} - {n.Filters.VoteCountEnd}");
        }

        if (o.Filters.OnlyOwnUploads != n.Filters.OnlyOwnUploads)
        {
            diff.Add($"Only own uploads: {o.Filters.OnlyOwnUploads} → {n.Filters.OnlyOwnUploads}");
        }

        if (o.Filters.ScreenshotKind != n.Filters.ScreenshotKind)
        {
            diff.Add(
                $"Screenshots: {o.Filters.ScreenshotKind.GetDescription()} → {n.Filters.ScreenshotKind.GetDescription()}");
        }

        if (o.ListDistributionKind != n.ListDistributionKind)
        {
            diff.Add(
                $"List distribution: {o.ListDistributionKind.GetDescription()} → {n.ListDistributionKind.GetDescription()}");
        }

        if (o.GamemodeKind != n.GamemodeKind)
        {
            diff.Add($"Game mode: {o.GamemodeKind.GetDescription()} → {n.GamemodeKind.GetDescription()}");
        }

        if (o.NGMCAllowBurning != n.NGMCAllowBurning)
        {
            diff.Add($"NGMC burning: {o.NGMCAllowBurning} → {n.NGMCAllowBurning}");
        }

        if (o.Filters.StartTimePercentageStart != n.Filters.StartTimePercentageStart ||
            o.Filters.StartTimePercentageEnd != n.Filters.StartTimePercentageEnd)
        {
            diff.Add(
                $"Sample point: {o.Filters.StartTimePercentageStart} - {o.Filters.StartTimePercentageEnd} → {n.Filters.StartTimePercentageStart} - {n.Filters.StartTimePercentageEnd}");
        }

        return diff;
    }
}
