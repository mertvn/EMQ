using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum LivesScoringKind
{
    // [Description("At most one life is lost if ANY guess type is incorrect")]
    Default,

    [Description("Each guess type takes one life")]
    EachGuessTypeTakesOneLife,

    // [Description("At most one life is lost if ALL guess types are incorrect")]
    // MinusOneIfAllIncorrect,
}
