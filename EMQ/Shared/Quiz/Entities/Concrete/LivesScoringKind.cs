using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum LivesScoringKind
{
    Default,

    [Description("Each guess type takes one life")]
    EachGuessTypeTakesOneLife,
}
