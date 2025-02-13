using System.ComponentModel;

namespace Juliet.Model.Filters;

public enum FilterField
{
    [Description("id")]
    Id,

    [Description("search")]
    Search,

    [Description("label")]
    Label,

    [Description("vn")]
    Vn,

    [Description("lang")]
    Lang,

    [Description("type")]
    Type,

    [Description("released")]
    Released,

    [Description("medium")]
    Medium,

    [Description("aid")]
    Aid,

    [Description("ismain")]
    IsMain,
}
