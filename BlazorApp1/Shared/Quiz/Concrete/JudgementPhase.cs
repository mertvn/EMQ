using BlazorApp1.Shared.Quiz.Abstract;

namespace BlazorApp1.Shared.Quiz.Concrete;

public class JudgementPhase : IQuizPhase
{
    public QuizPhaseKind Kind => QuizPhaseKind.Judgement;
}
