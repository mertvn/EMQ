using System;
using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[Flags]
public enum SongAttributes
{
    None = 0,

    [Description("Video contains spoilers")]
    Spoilers = 1,

    [Description("Non-canon")]
    NonCanon = 2,

    [Description("Unofficial")]
    Unofficial = 4,

    [Description("Video contains flashing lights")]
    FlashingLights = 8,
}
