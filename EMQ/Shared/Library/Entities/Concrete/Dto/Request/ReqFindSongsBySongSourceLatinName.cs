namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsBySongSourceTitle
{
    public ReqFindSongsBySongSourceTitle(string songSourceTitle)
    {
        SongSourceTitle = songSourceTitle;
    }

    public string SongSourceTitle { get; }
}
