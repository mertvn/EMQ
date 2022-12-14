namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqChangeRoomSettings
{
    public ReqChangeRoomSettings(string playerToken, int roomId, string roomPassword, QuizSettings quizSettings)
    {
        PlayerToken = playerToken;
        RoomPassword = roomPassword;
        QuizSettings = quizSettings;
        RoomId = roomId;
    }

    public string PlayerToken { get; }

    public int RoomId { get; }

    public string RoomPassword { get; }

    public QuizSettings QuizSettings { get; }
}
