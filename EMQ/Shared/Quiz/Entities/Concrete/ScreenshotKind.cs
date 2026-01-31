using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum ScreenshotKind
{
    None,

    [Display(Name = "sf")]
    VN,

    [Display(Name = "cv")]
    [Description("Source cover")]
    VNCover,

    [Display(Name = "ch")]
    Character,

    [Display(Name = "sf")]
    [Description("Source (prefer explicit)")]
    VNPreferExplicit,

    [Display(Name = "cv")]
    [Description("Source cover (blurred text)")]
    VNCoverBlurredText,
}
