using System;
using System.ComponentModel;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[Flags]
public enum SongAttributes
{
    None = 0,

    [Description("Video contains spoilers")]
    Spoilers = 1,
    NonCanon = 2,
    Unofficial = 4,
}
