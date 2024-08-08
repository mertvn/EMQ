using System;
using System.Collections.Generic;
using System.Linq;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongArtist
{
    public int Id { get; set; }

    public string? PrimaryLanguage { get; set; }

    public Sex Sex { get; set; } = Sex.Unknown;

    public string? VndbId { get; set; }

    public List<Title> Titles { get; set; } = new(); // todo should be singular

    public SongArtistRole Role { get; set; } = SongArtistRole.Unknown; // todo needs to be list

    public HashSet<int> MusicIds { get; set; } = new(); // todo? remove

    public override string ToString()
    {
        var first = Titles.FirstOrDefault(y => y.Language == "ja" && y.IsMainTitle) ?? Titles.First();
        return $"{first.LatinTitle}" +
               (!string.IsNullOrWhiteSpace(first.NonLatinTitle) && !string.Equals(first.NonLatinTitle, first.LatinTitle,
                   StringComparison.InvariantCultureIgnoreCase)
                   ? $" ({first.NonLatinTitle})"
                   : "");
    }
}

public enum Sex
{
    Unknown,
    Female,
    Male
}

public enum SongArtistRole
{
    Unknown,
    Vocals,
    Composer,
    Staff, // not sure what this role actually entails
    Translator,
}
