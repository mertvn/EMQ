using System;
using System.Collections.Generic;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongArtist
{
    public int Id { get; set; }

    public string? PrimaryLanguage { get; set; }

    public Sex Sex { get; set; } = Sex.Unknown;

    public List<Title> Titles { get; set; } = new();

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
