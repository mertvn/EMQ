namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class QuizSettings
{
    public int GuessTime { get; set; } = 20;

    public int ResultsTime { get; set; } = 20;

    public int PreloadAmount { get; set; } = 1;
}
