namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqCreateRoom
{
    public ReqCreateRoom(string playerToken, string name, string password, QuizSettings quizSettings)
    {
        PlayerToken = playerToken;
        Name = name;
        Password = password;
        QuizSettings = quizSettings;
    }

    public string PlayerToken { get; }

    public string Name { get; }

    public string Password { get; }

    public QuizSettings QuizSettings { get; }
}
