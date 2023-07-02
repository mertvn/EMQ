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

    [Description("producers.developer")]
    ProducersDeveloper,

    [Description("producers.publisher")]
    ProducersPublisher,

    [Description("producers.name")]
    ProducersName,

    [Description("producers.original")]
    ProducersOriginal,

    [Description("released")]
    Released,
}
