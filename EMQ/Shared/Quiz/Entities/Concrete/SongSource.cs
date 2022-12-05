using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongSource
{
    public int Id { get; set; }

    public DateTime AirDateStart { get; set; }

    public DateTime? AirDateEnd { get; set; }

    public string LanguageOriginal { get; set; } = "";

    public int? RatingAverage { get; set; }

    public SongSourceType Type { get; set; } = SongSourceType.Unknown;

    public List<Title> Titles { get; set; } = new();

    public List<SongSourceLink> Links { get; set; } = new();

    public List<SongSourceCategory> Categories { get; set; } = new();

    public List<SongSourceSongType> SongTypes { get; set; } = new();
}

public enum SongSourceSongType
{
    Unknown,
    OP,
    ED,
    Insert,
    BGM,
}

public enum SongSourceType
{
    Unknown,
    VN,
    Other
}
