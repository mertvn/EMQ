using System.ComponentModel;

namespace Juliet.Model.VNDBObject.Fields;

public enum FieldPOST_vn
{
    [Description("id")]
    Id,

    [Description("title")]
    Title,

    [Description("alttitle")]
    AltTitle,

    [Description("released")]
    Released,

    [Description("developers.id")]
    DevelopersId,

    [Description("developers.name")]
    DevelopersName,

    [Description("developers.original")]
    DevelopersOriginal,
}
