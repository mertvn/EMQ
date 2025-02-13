using System.ComponentModel;

namespace Juliet.Model.VNDBObject.Fields;

public enum FieldPOST_staff
{
    [Description("id")]
    Id,

    [Description("aid")]
    Aid,

    [Description("ismain")]
    IsMain,

    [Description("name")]
    Name,

    [Description("original")]
    Original,

    [Description("lang")]
    Lang,

    [Description("gender")]
    Gender,

    [Description("description")]
    Description,

    [Description("extlinks.url")]
    ExtlinksUrl,

    [Description("extlinks.label")]
    ExtlinksLabel,

    [Description("extlinks.name")]
    ExtlinksName,

    [Description("extlinks.id")]
    ExtlinksId,

    [Description("aliases.aid")]
    AliasesAid,

    [Description("aliases.name")]
    AliasesName,

    [Description("aliases.latin")]
    AliasesLatin,

    [Description("aliases.ismain")]
    AliasesIsMain,
}
