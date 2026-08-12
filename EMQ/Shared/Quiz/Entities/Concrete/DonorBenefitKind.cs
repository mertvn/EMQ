using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum DonorBenefitKind
{
    [Display(Name = "$1")]
    [Description("Donor badge")]
    DonorBadge,

    [Display(Name = "$5")]
    [Description("Username color")]
    UsernameColor,

    [Display(Name = "$10")]
    [Description("Username animation")]
    UsernameAnimation,

    [Display(Name = "$15")]
    [Description("Custom avatar image")]
    UploadedImageAvatar,
}
