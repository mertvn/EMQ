using System.ComponentModel;

namespace Juliet.Model.VNDBObject.Fields;

public enum FieldPOST_producer
{
    [Description("id")]
    Id,

    [Description("name")]
    Name,

    [Description("original")]
    Original,

    [Description("aliases")]
    Aliases,

    [Description("lang")]
    Lang,

    [Description("type")]
    Type,

    [Description("description")]
    Description,
}
