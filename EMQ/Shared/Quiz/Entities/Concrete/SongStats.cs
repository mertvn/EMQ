namespace EMQ.Shared.Quiz.Entities.Concrete;

public class SongStats
{
    public long TimesCorrect { get; set; }

    public long TimesPlayed { get; set; }

    public float CorrectPercentage { get; set; }

    public long TimesGuessed { get; set; }

    public long TotalGuessMs { get; set; }

    public int AverageGuessMs { get; set; }
}
