using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqReturnToRoom
{
    public ReqReturnToRoom(string playerToken, Guid roomId)
    {
        PlayerToken = playerToken;
        RoomId = roomId;
    }

    public string PlayerToken { get; }

    public Guid RoomId { get; }
}
