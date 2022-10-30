using EMQ.Shared.Quiz.Entities.Abstract;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class JudgementPhase : IQuizPhase
{
    public QuizPhaseKind Kind => QuizPhaseKind.Judgement;
}
