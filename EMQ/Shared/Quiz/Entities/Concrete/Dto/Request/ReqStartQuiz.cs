namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqStartQuiz
{
    public ReqStartQuiz(int playerId, int roomId)
    {
        PlayerId = playerId;
        RoomId = roomId;
    }

    public int PlayerId { get; }

    public int RoomId { get; }
}
