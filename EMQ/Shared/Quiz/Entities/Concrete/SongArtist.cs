using System;
using System.Collections.Generic;
using System.Linq;
using EMQ.Shared.Core;
using EMQ.Shared.Quiz.Entities.Abstract;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongArtist : IEditQueueEntity
{
    public int Id { get; set; }

    public string? PrimaryLanguage { get; set; }

    public Sex Sex { get; set; } = Sex.Unknown;

    // todo remove
    public string? VndbId => Links.SingleOrDefault(x => x.Type == SongArtistLinkType.VNDBStaff)?.Url.ToVndbId()
                             ?? Links.SingleOrDefault(x => x.Type == SongArtistLinkType.MusicBrainzArtist)?.Url
                                 .Replace("https://musicbrainz.org/artist/", "");

    public List<Title> Titles { get; set; } = new(); // todo should be singular

    public SongArtistRole Role { get; set; } = SongArtistRole.Unknown; // todo needs to be list

    public HashSet<int> MusicIds { get; set; } = new(); // todo? remove

    public List<SongArtistLink> Links { get; set; } = new();

    public override string ToString()
    {
        var first = Titles.FirstOrDefault(y => y.IsMainTitle) ?? Titles.First();
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
