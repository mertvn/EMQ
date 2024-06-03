using System;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class LabelStats
{
    public float CorrectPercentage { get; set; }

    public float GuessMs { get; set; }

    public float UniqueUsers { get; set; }

    public int TotalSongs { get; set; }

    public int TotalSources { get; set; }

    public int TotalArtists { get; set; }
}
