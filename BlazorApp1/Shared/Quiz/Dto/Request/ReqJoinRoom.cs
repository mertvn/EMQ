namespace BlazorApp1.Shared.Quiz.Dto.Request;

public class ReqJoinRoom
{
    public ReqJoinRoom(int roomId, string password, int playerId)
    {
        RoomId = roomId;
        Password = password;
        PlayerId = playerId;
    }

    public int RoomId { get; }

    public string Password { get; }

    public int PlayerId { get; } // todo ???
}
