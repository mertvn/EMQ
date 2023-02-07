namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqSendChatMessage
{
    public ReqSendChatMessage(string playerToken, string contents)
    {
        PlayerToken = playerToken;
        Contents = contents;
    }

    public string PlayerToken { get; set; }

    public string Contents { get; set; }
}
