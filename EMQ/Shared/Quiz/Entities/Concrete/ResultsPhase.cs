using EMQ.Shared.Quiz.Entities.Abstract;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class ResultsPhase : IQuizPhase
{
    public QuizPhaseKind Kind => QuizPhaseKind.Results;
}
