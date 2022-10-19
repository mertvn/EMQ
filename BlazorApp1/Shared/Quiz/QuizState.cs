using System.Text.Json.Serialization;
using BlazorApp1.Shared.Quiz.Abstract;
using BlazorApp1.Shared.Quiz.Concrete;

namespace BlazorApp1.Shared.Quiz;

public class QuizState
{
    public bool IsActive { get; set; }

    [JsonIgnore] public IQuizPhase Phase { get; set; } = new GuessPhase();

    // public int ElapsedSeconds { get; set; }

    public int RemainingSeconds { get; set; }

    // "Song Pointer" (Current Song Index)
    public int sp { get; set; } = -1;

    public int NumSongs { get; set; } = 9; // todo hook this up
}
