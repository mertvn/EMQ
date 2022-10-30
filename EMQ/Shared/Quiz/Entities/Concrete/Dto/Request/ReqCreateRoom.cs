namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqCreateRoom
{
    public ReqCreateRoom(int playerId, string name, string password, QuizSettings quizSettings)
    {
        PlayerId = playerId;
        Name = name;
        Password = password;
        QuizSettings = quizSettings;
    }

    public int PlayerId { get; }

    public string Name { get; }

    public string Password { get; }

    public QuizSettings QuizSettings { get; }
}
