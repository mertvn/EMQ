using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongArtist
{
    public int Id { get; set; }

    public string? PrimaryLanguage { get; set; }

    public Sex Sex { get; set; } = Sex.Unknown;

    public string? VndbId { get; set; }

    public List<Title> Titles { get; set; } = new(); // todo should be singular

    public SongArtistRole Role { get; set; } = SongArtistRole.Unknown;
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
}
