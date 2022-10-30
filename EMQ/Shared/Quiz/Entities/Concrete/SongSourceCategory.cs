using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongSourceCategory
{
    public string Name { get; set; } = "";

    public SongSourceCategoryType Type { get; set; } = SongSourceCategoryType.Unknown;
}

public enum SongSourceCategoryType
{
    Unknown,
    Tag,
    Genre
}
