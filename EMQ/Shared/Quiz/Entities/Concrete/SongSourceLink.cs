using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongSourceLink
{
    public string Url { get; set; } = "";

    public SongSourceLinkType Type { get; set; } = SongSourceLinkType.Unknown;
}

public enum SongSourceLinkType
{
    Unknown,
    VNDB,
}
