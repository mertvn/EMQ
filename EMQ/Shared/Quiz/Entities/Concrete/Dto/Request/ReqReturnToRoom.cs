namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqReturnToRoom
{
    public ReqReturnToRoom(string playerToken, int roomId)
    {
        PlayerToken = playerToken;
        RoomId = roomId;
    }

    public string PlayerToken { get; }

    public int RoomId { get; }
}
