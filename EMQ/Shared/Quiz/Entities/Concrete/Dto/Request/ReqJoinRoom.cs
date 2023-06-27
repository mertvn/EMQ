using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqJoinRoom
{
    public ReqJoinRoom(Guid roomId, string password, string playerToken)
    {
        RoomId = roomId;
        Password = password;
        PlayerToken = playerToken;
    }

    public Guid RoomId { get; }

    public string Password { get; }

    public string PlayerToken { get; }
}
