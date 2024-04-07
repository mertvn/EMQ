using System.ComponentModel;

namespace Juliet.Model.VNDBObject.Fields;

public enum FieldPOST_release
{
    [Description("id")]
    Id,

    [Description("title")]
    Title,

    [Description("alttitle")]
    AltTitle,

    [Description("vns.rtype")]
    VNsRType,

    [Description("vns.id")]
    VNsId,

    [Description("producers.developer")]
    ProducersDeveloper,

    [Description("producers.publisher")]
    ProducersPublisher,

    [Description("producers.id")]
    ProducersId,

    [Description("producers.name")]
    ProducersName,

    [Description("producers.original")]
    ProducersOriginal,

    [Description("released")]
    Released,

    // todo media
    // [Description("medium")]
    // Medium,
}
