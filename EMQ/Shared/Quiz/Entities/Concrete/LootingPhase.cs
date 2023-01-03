using EMQ.Shared.Quiz.Entities.Abstract;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class LootingPhase : IQuizPhase
{
    public QuizPhaseKind Kind => QuizPhaseKind.Looting;
}
