using System.ComponentModel;

namespace Juliet.Model.Filters;

public enum FilterOperator
{
    [Description("=")]
    Equal,

    [Description("!=")]
    NotEqual,

    [Description(">=")]
    GreaterEqual,

    [Description(">")]
    GreaterThan,

    [Description("<=")]
    LesserEqual,

    [Description("<")]
    LessThan
}
