using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum ListDistributionKind
{
    Random,
    Balanced,

    [Description("Balanced (strict)")]
    BalancedStrict,
}
