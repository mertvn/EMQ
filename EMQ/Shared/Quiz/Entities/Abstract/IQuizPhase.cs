using EMQ.Shared.Quiz.Entities.Concrete;

namespace EMQ.Shared.Quiz.Entities.Abstract;

public interface IQuizPhase
{
    public QuizPhaseKind Kind { get; }
}
