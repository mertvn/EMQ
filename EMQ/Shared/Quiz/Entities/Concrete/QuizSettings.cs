namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizSettings
{
    public int NumSongs { get; set; } = 100;

    public int GuessTime { get; set; } = 20;

    public int ResultsTime { get; set; } = 20;

    public int PreloadAmount { get; set; } = 1;

    public bool IsHotjoinEnabled { get; set; } = true;
}
