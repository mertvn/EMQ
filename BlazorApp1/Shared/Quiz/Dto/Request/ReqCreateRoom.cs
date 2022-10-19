namespace BlazorApp1.Shared.Quiz.Dto.Request;

public class ReqCreateRoom
{
    public ReqCreateRoom(string name, string password, QuizSettings quizSettings)
    {
        Name = name;
        Password = password;
        QuizSettings = quizSettings;
    }

    public string Name { get; }

    public string Password { get; }

    public QuizSettings QuizSettings { get; }
}
