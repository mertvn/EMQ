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
}
