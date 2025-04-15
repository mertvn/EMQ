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

    [Description("olang")]
    Olang,

    [Description("average")]
    Average,

    [Description("rating")]
    Rating,

    [Description("votecount")]
    VoteCount,

    [Description("titles.lang")]
    TitlesLang,

    [Description("titles.title")]
    TitlesTitle,

    [Description("titles.latin")]
    TitlesLatin,

    [Description("titles.main")]
    TitlesMain,

    [Description("aliases")]
    Aliases,

    [Description("developers.id")]
    DevelopersId,

    [Description("developers.name")]
    DevelopersName,

    [Description("developers.original")]
    DevelopersOriginal,

    [Description("developers.aliases")]
    DevelopersAliases,

    [Description("developers.lang")]
    DevelopersLang,

    [Description("developers.type")]
    DevelopersType,

    [Description("developers.description")]
    DevelopersDescription,

    [Description("extlinks.url")]
    ExtlinksUrl,

    [Description("extlinks.label")]
    ExtlinksLabel,

    [Description("extlinks.name")]
    ExtlinksName,

    [Description("extlinks.id")]
    ExtlinksId,
}
