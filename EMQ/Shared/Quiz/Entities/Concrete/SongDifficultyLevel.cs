using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum SongDifficultyLevel
{
    [Range(55.01d, 100d)]
    [Display(Name = "Very Easy")]
    VeryEasy,

    [Range(33.01d, 55d)]
    [Display(Name = "Easy")]
    Easy,

    [Range(19.01d, 33d)]
    [Display(Name = "Medium")]
    Medium,

    [Range(7.01d, 19d)]
    [Display(Name = "Hard")]
    Hard,

    [Range(0.01d, 7d)]
    [Display(Name = "Very Hard")]
    VeryHard,

    [Range(0d, 0d)]
    [Display(Name = "Impossible")]
    Impossible
}
