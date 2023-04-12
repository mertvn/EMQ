namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Response;

public class ResJoinRoom
{
    public ResJoinRoom(QuizStatus quizStatus)
    {
        QuizStatus = quizStatus;
    }

    public QuizStatus QuizStatus { get; }
}
