using System;

namespace EMQ.Shared.Quiz.Entities.Concrete;

[Flags]
public enum SongAttributes
{
    None = 0,
    Spoilers = 1,
    NonCanon = 2,
    Unofficial = 4,
}
