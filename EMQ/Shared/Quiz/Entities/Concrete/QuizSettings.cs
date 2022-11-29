namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizSettings
{
    public int NumSongs { get; set; } = 100;

    public int GuessMs { get; set; } = 20000;

    public int ResultsMs { get; set; } = 20000;

    public int PreloadAmount { get; set; } = 1;

    public bool IsHotjoinEnabled { get; set; } = true;

    public int TeamSize { get; set; } = 1;

    // public bool Duplicates { get; set; } = true; // TODO

    public int MaxLives { get; set; }  // TODO
}
