namespace BlazorApp1.Shared.Quiz;

public class QuizState
{
    public bool Active { get; set; }

    public int Phase { get; set; } // 0: guess 1: waiting for judgement 2: results // todo: abstraction

    // public int ElapsedSeconds { get; set; }

    public int RemainingSeconds { get; set; }

    // "Song Pointer" (Current Song Index)
    public int sp { get; set; } = -1;

    public int NumSongs { get; set; } = 10; // todo hook this up
}
