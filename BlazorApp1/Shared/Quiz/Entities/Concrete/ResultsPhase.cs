using BlazorApp1.Shared.Quiz.Entities.Abstract;

namespace BlazorApp1.Shared.Quiz.Entities.Concrete;

public class ResultsPhase : IQuizPhase
{
    public QuizPhaseKind Kind => QuizPhaseKind.Results;
}
