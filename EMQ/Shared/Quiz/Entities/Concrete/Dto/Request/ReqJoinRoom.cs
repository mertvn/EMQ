using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqJoinRoom
{
    public ReqJoinRoom(Guid roomId, string password, int playerId)
    {
        RoomId = roomId;
        Password = password;
        PlayerId = playerId;
    }

    public Guid RoomId { get; }

    public string Password { get; }

    public int PlayerId { get; } // todo ???
}
