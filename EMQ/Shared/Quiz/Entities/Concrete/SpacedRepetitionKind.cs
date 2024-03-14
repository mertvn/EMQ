using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum SpacedRepetitionKind
{
    Review = 0,

    [Description("Songs without intervals")]
    NoIntervalOnly = 1,
}
