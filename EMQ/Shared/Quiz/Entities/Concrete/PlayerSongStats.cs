using EMQ.Shared.Core;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class PlayerSongStats
{
    public int UserId { get; set; }

    public int MusicId { get; set; }

    public long TimesCorrect { get; set; }

    public long TimesPlayed { get; set; }

    public float CorrectPercentage => ((float)TimesCorrect).Div0(TimesPlayed) * 100;

    public long TimesGuessed { get; set; }

    public long TotalGuessMs { get; set; }

    public int AverageGuessMs => (int)((float)TotalGuessMs).Div0(TimesGuessed);

    public string Username { get; set; } = "";
}
