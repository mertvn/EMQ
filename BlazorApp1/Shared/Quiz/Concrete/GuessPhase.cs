using BlazorApp1.Shared.Quiz.Abstract;

namespace BlazorApp1.Shared.Quiz.Concrete;

public class GuessPhase : IQuizPhase
{
    public QuizPhaseKind Kind { get; } = QuizPhaseKind.Guess;
}
