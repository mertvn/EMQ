using EMQ.Shared.Quiz.Entities.Abstract;

namespace EMQ.Shared.Quiz.Entities.Concrete;

public class GuessPhase : IQuizPhase
{
    public QuizPhaseKind Kind => QuizPhaseKind.Guess;
}
