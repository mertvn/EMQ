namespace EMQ.Shared.Quiz.Entities.Concrete;

public class QuizState
{
    public QuizStatus QuizStatus { get; set; } = QuizStatus.Starting;

    public QuizPhaseKind Phase { get; set; } = QuizPhaseKind.Guess;

    /// <summary>
    ///  The remaining time for current phase in milliseconds
    /// </summary>
    public float RemainingMs { get; set; }

    /// <summary>
    ///  "Song Pointer" (a.k.a. Current Song Index)
    /// </summary>
    public int sp { get; set; } = -1;

    public int NumSongs { get; set; }

    public bool IsPaused { get; set; }

    public string ExtraInfo { get; set; } = "";

    public int LootingGridSize { get; set; }
}
