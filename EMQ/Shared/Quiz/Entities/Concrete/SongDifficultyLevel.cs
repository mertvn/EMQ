using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum SongDifficultyLevel
{
    [Range(70.01d, 100d)]
    [Display(Name = "Very Easy")]
    VeryEasy,

    [Range(50.01d, 70d)]
    [Display(Name = "Easy")]
    Easy,

    [Range(20.01d, 50d)]
    [Display(Name = "Medium")]
    Medium,

    [Range(10.01d, 20d)]
    [Display(Name = "Hard")]
    Hard,

    [Range(0.01d, 10d)]
    [Display(Name = "Very Hard")]
    VeryHard,

    [Range(0d, 0d)]
    [Display(Name = "Impossible")]
    Impossible
}
