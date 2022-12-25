using System.ComponentModel;

namespace Juliet.Model.Filters;

public enum CombinatorKind
{
    [Description("and")]
    And,

    [Description("or")]
    Or
}
