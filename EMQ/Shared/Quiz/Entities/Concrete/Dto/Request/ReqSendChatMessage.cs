namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqSendChatMessage
{
    public ReqSendChatMessage(string playerToken, int roomId, string contents)
    {
        PlayerToken = playerToken;
        RoomId = roomId;
        Contents = contents;
    }

    public string PlayerToken { get; set; }

    public int RoomId { get; set; }

    public string Contents { get; set; }
}
