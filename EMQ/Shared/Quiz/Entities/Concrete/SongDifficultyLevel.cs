using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum SongDifficultyLevel
{
    [Range(75, 100)]
    [Display(Name = "Very Easy")]
    VeryEasy,

    [Range(55, 75)]
    [Display(Name = "Easy")]
    Easy,

    [Range(35, 55)]
    [Display(Name = "Medium")]
    Medium,

    [Range(15, 35)]
    [Display(Name = "Hard")]
    Hard,

    [Range(0, 15)]
    [Display(Name = "Very Hard")]
    VeryHard,

    [Range(0, 0)]
    [Display(Name = "Impossible")]
    Impossible
}
