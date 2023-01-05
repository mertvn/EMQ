using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizSettings
{
    [Required]
    [Range(1, 100)]
    [DefaultValue(40)]
    public int NumSongs { get; set; } = 40;

    [Required]
    [Range(10000, 60000)]
    [DefaultValue(25000)]
    public int GuessMs { get; set; } = 25000;

    [Required]
    [Range(10000, 60000)]
    [DefaultValue(25000)]
    public int ResultsMs { get; set; } = 25000;

    [Required]
    [Range(1, 1)]
    [DefaultValue(1)]
    public int PreloadAmount { get; set; } = 1;

    [Required]
    [DefaultValue(true)]
    public bool IsHotjoinEnabled { get; set; } = true;

    [Required]
    [Range(1, 8)]
    [DefaultValue(1)]
    public int TeamSize { get; set; } = 1;

    // public bool Duplicates { get; set; } = true; // TODO

    [Required]
    [Range(0, 5)]
    [DefaultValue(0)]
    public int MaxLives { get; set; } = 0;

    [Required]
    [DefaultValue(true)]
    public bool OnlyFromLists { get; set; } = true;

    [Required]
    [DefaultValue(SongSelectionKind.Random)]
    public SongSelectionKind SongSelectionKind { get; set; } = SongSelectionKind.Random;

    [Required]
    [Range(20000, 200000)]
    [DefaultValue(120000)]
    public int LootingMs { get; set; } = 120000;

    [Required]
    [Range(1, 10)]
    [DefaultValue(5)]
    public int InventorySize { get; set; } = 5;
}
