using System.ComponentModel;

namespace Juliet.Model.VNDBObject.Fields;

public enum FieldPOST_ulist
{
    [Description("added")]
    Added,

    [Description("vote")]
    Vote,

    [Description("labels.id")]
    LabelsId,

    [Description("labels.label")]
    LabelsLabel,
}
