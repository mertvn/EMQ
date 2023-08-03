using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqChangeRoomSettings
{
    public ReqChangeRoomSettings(string playerToken, Guid roomId, QuizSettings quizSettings)
    {
        PlayerToken = playerToken;
        RoomId = roomId;
        QuizSettings = quizSettings;
    }

    public string PlayerToken { get; }

    public Guid RoomId { get; }

    public QuizSettings QuizSettings { get; }
}
