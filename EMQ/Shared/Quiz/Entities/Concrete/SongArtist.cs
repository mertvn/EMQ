using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
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

    public List<Title> Titles { get; set; } = new();

    public List<SongArtistRole> Roles { get; set; } = new();

    public List<SongArtistLink> Links { get; set; } = new();

    public List<ArtistArtist> ArtistArtists { get; set; } = new();

    public override string ToString()
    {
        var first = Titles.FirstOrDefault(y => y.IsMainTitle) ?? Titles.First();
        return $"{first.LatinTitle}" +
               (!string.IsNullOrWhiteSpace(first.NonLatinTitle) && !string.Equals(first.NonLatinTitle, first.LatinTitle,
                   StringComparison.InvariantCultureIgnoreCase)
                   ? $" ({first.NonLatinTitle})"
                   : "");
    }

    /// NOT [Pure]
    public SongArtist Sort()
    {
        Titles = Titles.OrderBy(x => x.LatinTitle).ThenBy(x => x.NonLatinTitle).ToList();
        Links = Links.OrderBy(x => x.Url).ToList();
        Roles.Sort();
        return this;
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
    Unknown = 0,
    Vocals = 1,
    Composer = 2,

    // Staff = 3, // not sure what this role actually entails
    // Translator = 4,
    Arranger = 5,
    Lyricist = 6,
}

public enum ArtistArtistRelKind
{
    [Display(Name = "Member of band")]
    MemberOfBand = 103, // https://musicbrainz.org/relationship/5be4c609-9afa-4ea0-910b-12ffb71e3821
}
