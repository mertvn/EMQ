using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum SongDifficultyLevel
{
    [Range(70, 100)]
    [Display(Name = "Very Easy")]
    VeryEasy,

    [Range(50, 70)]
    [Display(Name = "Easy")]
    Easy,

    [Range(30, 50)]
    [Display(Name = "Medium")]
    Medium,

    [Range(15, 30)]
    [Display(Name = "Hard")]
    Hard,

    [Range(0, 15)]
    [Display(Name = "Very Hard")]
    VeryHard,

    [Range(0, 0)]
    [Display(Name = "Impossible")]
    Impossible
}
