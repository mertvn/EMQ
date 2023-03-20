using System;
using System.Collections.Generic;
using System.Linq;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongSource
{
    public int Id { get; set; }

    public DateTime AirDateStart { get; set; }

    public DateTime? AirDateEnd { get; set; }

    public string LanguageOriginal { get; set; } = "";

    public int? RatingAverage { get; set; }

    public int? RatingBayesian { get; set; }

    public int? Popularity { get; set; }

    public int? VoteCount { get; set; }

    public SongSourceType Type { get; set; } = SongSourceType.Unknown;

    public List<Title> Titles { get; set; } = new();

    public List<SongSourceLink> Links { get; set; } = new();

    public List<SongSourceCategory> Categories { get; set; } = new();

    public List<SongSourceSongType> SongTypes { get; set; } = new();

    public HashSet<int> MusicIds { get; set; } = new();

    public override string ToString() =>
        $"{Titles.First().LatinTitle}" + (!string.IsNullOrWhiteSpace(Titles.First().NonLatinTitle)
            ? $" ({Titles.First().NonLatinTitle})"
            : "");
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
