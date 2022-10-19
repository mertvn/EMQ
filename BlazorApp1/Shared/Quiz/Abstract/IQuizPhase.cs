using BlazorApp1.Shared.Quiz.Concrete;

namespace BlazorApp1.Shared.Quiz.Abstract;

public interface IQuizPhase
{
    public QuizPhaseKind Kind { get; }
}
