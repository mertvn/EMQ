using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum DonorBenefitKind
{
    [Description("Donor badge")]
    DonorBadge,

    [Description("Username color")]
    UsernameColor,

    [Description("Username animation")]
    UsernameAnimation,
}
