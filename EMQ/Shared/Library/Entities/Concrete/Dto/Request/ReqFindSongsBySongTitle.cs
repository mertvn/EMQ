namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsBySongTitle
{
    public ReqFindSongsBySongTitle(string songTitle)
    {
        SongTitle = songTitle;
    }

    public string SongTitle { get; }
}
