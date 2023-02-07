namespace EMQ.Shared.Quiz.Entities.Concrete.Dto.Request;

public class ReqNextSong
{
    public ReqNextSong(string playerToken, int songIndex)
    {
        PlayerToken = playerToken;
        SongIndex = songIndex;
    }

    public string PlayerToken { get; }

    public int SongIndex { get; }
}
