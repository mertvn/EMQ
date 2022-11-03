namespace EMQ.Shared.Library.Entities.Concrete.Dto.Request;

public class ReqFindSongsBySongSourceLatinTitle
{
    public ReqFindSongsBySongSourceLatinTitle(string songSourceLatinTitle)
    {
        SongSourceLatinTitle = songSourceLatinTitle;
    }

    public string SongSourceLatinTitle { get; }
}
