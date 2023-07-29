using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum Language
{
    [Display(Name = "All languages")]
    allLanguages,

    [Display(Name = "Japanese")]
    ja,

    [Display(Name = "English")]
    en,

    [Description("zh-Hans")]
    [Display(Name = "Chinese (simplified)")]
    zhHans,

    [Description("zh-Hant")]
    [Display(Name = "Chinese (traditional)")]
    zhHant,

    [Display(Name = "Korean")]
    ko,
}
