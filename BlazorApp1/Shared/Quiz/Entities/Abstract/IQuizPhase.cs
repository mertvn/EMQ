using BlazorApp1.Shared.Quiz.Entities.Concrete;

namespace BlazorApp1.Shared.Quiz.Entities.Abstract;

public interface IQuizPhase
{
    public QuizPhaseKind Kind { get; }
}
