using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongSourceCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string? VndbId { get; set; }

    public SongSourceCategoryType Type { get; set; } = SongSourceCategoryType.Unknown;

    public float? Rating { get; set; }

    public SpoilerLevel? SpoilerLevel { get; set; }
}

public enum SpoilerLevel
{
    None = 0,
    Minor = 1,
    Major = 2,
}

public enum SongSourceCategoryType
{
    Unknown,
    Tag,
    Genre
}
