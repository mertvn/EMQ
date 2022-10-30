namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqNextSong
{
    public ReqNextSong(int roomId, int songIndex)
    {
        RoomId = roomId;
        SongIndex = songIndex;
    }

    public int RoomId { get; }

    public int SongIndex { get; }
}
