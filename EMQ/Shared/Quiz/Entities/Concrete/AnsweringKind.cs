using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public enum AnsweringKind
{
    Typing,

    [Description("Multiple Choice")]
    MultipleChoice,
}
