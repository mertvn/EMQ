using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizSettings
{
    [Required]
    [Range(1, 100)]
    [DefaultValue(40)]
    public int NumSongs { get; set; } = 40;

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

    [Required]
    [Range(1, 1)]
    [DefaultValue(1)]
    public int PreloadAmount { get; set; } = 1;

    [Required]
    [DefaultValue(true)]
    public bool IsHotjoinEnabled { get; set; } = true;

    [Required]
    [Range(1, 777)]
    [DefaultValue(1)]
    public int TeamSize { get; set; } = 1;

    [Required]
    [DefaultValue(false)]
    public bool Duplicates { get; set; } = false;

    [Required]
    [Range(0, 9)]
    [DefaultValue(0)]
    public int MaxLives { get; set; } = 0;

    [Required]
    [DefaultValue(true)]
    public bool OnlyFromLists { get; set; } = true;

    [Required]
    [DefaultValue(SongSelectionKind.Random)]
    public SongSelectionKind SongSelectionKind { get; set; } = SongSelectionKind.Random;

    [Required]
    [DefaultValue(AnsweringKind.Typing)]
    public AnsweringKind AnsweringKind { get; set; } = AnsweringKind.Typing;

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

    [Required]
    [Range(1, 25)]
    [DefaultValue(7)]
    public int InventorySize { get; set; } = 7;

    [Required]
    [Range(51, 100)]
    [DefaultValue(90)]
    public int WaitPercentage { get; set; } = 90;

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

    [Required]
    public QuizFilters Filters { get; set; } = new();

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
}
