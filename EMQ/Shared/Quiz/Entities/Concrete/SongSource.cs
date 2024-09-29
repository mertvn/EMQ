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

    // public int? Popularity { get; set; }

    public int? VoteCount { get; set; }

    public SongSourceType Type { get; set; } = SongSourceType.Unknown;

    public List<Title> Titles { get; set; } = new();

    public List<SongSourceLink> Links { get; set; } = new();

    public List<SongSourceCategory> Categories { get; set; } = new();

    // todo refactor into Dictionary<int, List<SongSourceSongType>> and get rid of MusicIds
    public List<SongSourceSongType> SongTypes { get; set; } = new();

    // todo? the hashset might need sorting
    public Dictionary<int, HashSet<SongSourceSongType>> MusicIds { get; set; } = new();

    public List<SongSourceDeveloper> Developers { get; set; } = new();

    public override string ToString()
    {
        var first = Titles.FirstOrDefault(y => y.Language == "ja" && y.IsMainTitle) ?? Titles.First();
        return $"{first.LatinTitle}" +
               (!string.IsNullOrWhiteSpace(first.NonLatinTitle) && !string.Equals(first.NonLatinTitle, first.LatinTitle,
                   StringComparison.InvariantCultureIgnoreCase)
                   ? $" ({first.NonLatinTitle})"
                   : "");
    }

    /// NOT [Pure]
    public SongSource Sort()
    {
        Titles = Titles.OrderBy(x => x.LatinTitle).ThenBy(x => x.NonLatinTitle).ToList();
        Links = Links.OrderBy(x => x.Url).ToList();
        Categories = Categories.OrderBy(x => x.Id).ToList();
        Developers = Developers.OrderBy(x => x.VndbId).ToList();
        // todo MusicIds
        SongTypes.Sort();
        return this;
    }
}

public enum SongSourceSongType
{
    Unknown,
    OP,
    ED,
    Insert,
    BGM,
    Other = 600,
    Random = 777, // must be higher than everything else for song selection to work correctly
}

public enum SongSourceSongTypeMode
{
    All,
    Vocals,
    BGM,
}

public enum SongSourceType
{
    Unknown,
    VN,
    Other
}

public class SongSourceDeveloper
{
    public string VndbId { get; set; } = "";

    public Title Title { get; set; } = new();
}
