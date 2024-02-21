using System.ComponentModel.DataAnnotations;

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

    [Required]
    public string PlayerToken { get; }

    [Required]
    [MaxLength(78)]
    public string Name { get; }

    [MaxLength(16)]
    public string Password { get; }

    [Required]
    public QuizSettings QuizSettings { get; }
}
