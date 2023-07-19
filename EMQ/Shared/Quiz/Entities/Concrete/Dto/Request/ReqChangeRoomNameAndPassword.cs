using System;

namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqChangeRoomNameAndPassword
{
    public ReqChangeRoomNameAndPassword(string playerToken, Guid roomId, string newName, string newPassword)
    {
        PlayerToken = playerToken;
        RoomId = roomId;
        NewName = newName;
        NewPassword = newPassword;
    }

    public string PlayerToken { get; }

    public Guid RoomId { get; }

    public string NewName { get; }

    public string NewPassword { get; }
}
