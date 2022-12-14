namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqStartQuiz
{
    public ReqStartQuiz(string playerToken, int roomId)
    {
        PlayerToken = playerToken;
        RoomId = roomId;
    }

    public string PlayerToken { get; }

    public int RoomId { get; }
}
