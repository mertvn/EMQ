using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqChangeRoomPassword
{
    public ReqChangeRoomPassword(string playerToken, Guid roomId, string newPassword)
    {
        PlayerToken = playerToken;
        RoomId = roomId;
        NewPassword = newPassword;
    }

    public string PlayerToken { get; }

    public Guid RoomId { get; }

    public string NewPassword { get; }
}
